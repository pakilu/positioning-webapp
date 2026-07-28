using System.IO.Ports;
using System.Text.Json;
using App.BLL.Positioning;
using Microsoft.Extensions.Options;
using WebApp.Models.Mqtt;
using WebApp.Models.Serial;

namespace WebApp.Services;

public class SerialIngestService : BackgroundService
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private readonly ILogger<SerialIngestService> _logger;
    private readonly SerialOptions _options;
    private readonly IngestProcessor _ingest;
    private readonly IServiceScopeFactory _scopeFactory;
    private SerialPort? _port;

    public SerialIngestService(
        ILogger<SerialIngestService> logger,
        IOptions<SerialOptions> options,
        IngestProcessor ingest,
        IServiceScopeFactory scopeFactory)
    {
        _logger = logger;
        _options = options.Value;
        _ingest = ingest;
        _scopeFactory = scopeFactory;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.Enabled)
        {
            _logger.LogInformation("Serial ingest service is disabled.");
            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var port = new SerialPort(_options.PortName, _options.BaudRate)
                {
                    NewLine = "\n",
                    ReadTimeout = _options.ReadTimeoutMs,
                    DtrEnable = true,
                    RtsEnable = true,
                };

                _port = port;
                port.Open();
                _logger.LogInformation(
                    "Serial ingest service opened {PortName} at {BaudRate} baud.",
                    _options.PortName,
                    _options.BaudRate);

                while (!stoppingToken.IsCancellationRequested && port.IsOpen)
                {
                    try
                    {
                        var line = port.ReadLine();
                        await HandleLineAsync(line);
                    }
                    catch (TimeoutException)
                    {
                        // Periodically wake so cancellation is observed promptly.
                    }
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    ex,
                    "Serial ingest failed on {PortName}. Retrying in {DelaySeconds}s.",
                    _options.PortName,
                    _options.ReconnectDelaySeconds);
            }
            finally
            {
                ClosePort();
            }

            await Task.Delay(TimeSpan.FromSeconds(_options.ReconnectDelaySeconds), stoppingToken);
        }
    }

    public override Task StopAsync(CancellationToken cancellationToken)
    {
        ClosePort();
        return base.StopAsync(cancellationToken);
    }

    private async Task HandleLineAsync(string line)
    {
        var payload = line.Trim();
        if (string.IsNullOrWhiteSpace(payload)) return;
        if (!payload.StartsWith("{"))
        {
            _logger.LogDebug("Ignoring non-JSON serial line from {PortName}: {Payload}", _options.PortName, payload);
            return;
        }

        _logger.LogDebug("Serial [{PortName}] {Payload}", _options.PortName, payload);

        try
        {
            using var doc = JsonDocument.Parse(payload);
            var root = doc.RootElement;

            if (await TryHandleRoutingRequestAsync(root))
            {
                return;
            }

            if (LooksLikeRawMeasurement(root))
            {
                var msg = JsonSerializer.Deserialize<RawMeasurementMessage>(payload, JsonOpts);
                if (msg is not null)
                {
                    await _ingest.HandleRawAsync(msg, _options.PersistToDatabase);
                }
                return;
            }

            await _ingest.HandleRegistrationAsync(payload);
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Malformed JSON from serial port {PortName}: {Payload}", _options.PortName, payload);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to handle serial payload from {PortName}: {Payload}", _options.PortName, payload);
        }
    }

    private async Task<bool> TryHandleRoutingRequestAsync(JsonElement root)
    {
        if (!TryGetString(root, "command", out var command)
            && !TryGetString(root, "type", out command))
        {
            return false;
        }

        if (!string.Equals(command, "nextAnchors", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(command, "anchorRouting", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (!TryGetGuid(root, "sessionId", out var sessionId)
            || !TryGetString(root, "tagDeviceId", out var tagDeviceId))
        {
            WriteSerialLine(new
            {
                type = "nextAnchors",
                ok = false,
                error = "sessionId and tagDeviceId are required",
            });
            return true;
        }

        var count = TryGetInt(root, "count", out var requestedCount)
            ? requestedCount
            : AnchorRoutingService.DefaultAnchorCount;

        using var scope = _scopeFactory.CreateScope();
        var routing = scope.ServiceProvider.GetRequiredService<IAnchorRoutingService>();
        var decision = await routing.GetNextAnchorsAsync(sessionId, tagDeviceId, count);

        if (decision is null)
        {
            WriteSerialLine(new
            {
                type = "nextAnchors",
                ok = false,
                error = "No routing decision available",
            });
            return true;
        }

        WriteSerialLine(new
        {
            type = "nextAnchors",
            ok = true,
            decision.SessionId,
            decision.TagDeviceIdentifier,
            decision.PositionKnown,
            decision.MovementKnown,
            anchors = decision.Anchors.Select(a => new
            {
                a.DeviceIdentifier,
                a.Name,
                a.X,
                a.Y,
                a.Z,
            }),
        });
        return true;
    }

    private static bool LooksLikeRawMeasurement(JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Object) return false;

        return HasProperty(root, "distance")
               || HasProperty(root, "tagDeviceId")
               || HasProperty(root, "anchorDeviceId");
    }

    private static bool HasProperty(JsonElement root, string name)
    {
        foreach (var property in root.EnumerateObject())
        {
            if (string.Equals(property.Name, name, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static bool TryGetString(JsonElement root, string name, out string value)
    {
        value = "";
        if (root.ValueKind != JsonValueKind.Object) return false;

        foreach (var property in root.EnumerateObject())
        {
            if (string.Equals(property.Name, name, StringComparison.OrdinalIgnoreCase)
                && property.Value.ValueKind == JsonValueKind.String)
            {
                value = property.Value.GetString()?.Trim() ?? "";
                return value.Length > 0;
            }
        }

        return false;
    }

    private static bool TryGetGuid(JsonElement root, string name, out Guid value)
    {
        value = Guid.Empty;
        return TryGetString(root, name, out var text) && Guid.TryParse(text, out value);
    }

    private static bool TryGetInt(JsonElement root, string name, out int value)
    {
        value = 0;
        if (root.ValueKind != JsonValueKind.Object) return false;

        foreach (var property in root.EnumerateObject())
        {
            if (!string.Equals(property.Name, name, StringComparison.OrdinalIgnoreCase))
                continue;

            return property.Value.ValueKind == JsonValueKind.Number
                && property.Value.TryGetInt32(out value);
        }

        return false;
    }

    private void WriteSerialLine<T>(T payload)
    {
        var port = _port;
        if (port?.IsOpen != true)
        {
            _logger.LogWarning("Cannot write serial response because {PortName} is not open.", _options.PortName);
            return;
        }

        try
        {
            port.WriteLine(JsonSerializer.Serialize(payload, JsonOpts));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to write serial response to {PortName}.", _options.PortName);
        }
    }

    private void ClosePort()
    {
        try
        {
            if (_port?.IsOpen == true)
            {
                _port.Close();
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Error while closing serial port {PortName}.", _options.PortName);
        }
        finally
        {
            _port = null;
        }
    }
}
