using System.IO.Ports;
using System.Text.Json;
using Microsoft.Extensions.Options;
using WebApp.Models.Mqtt;
using WebApp.Models.Serial;

namespace WebApp.Services;

public class SerialIngestService : BackgroundService
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private readonly ILogger<SerialIngestService> _logger;
    private readonly SerialOptions _options;
    private readonly IngestProcessor _ingest;
    private SerialPort? _port;

    public SerialIngestService(
        ILogger<SerialIngestService> logger,
        IOptions<SerialOptions> options,
        IngestProcessor ingest)
    {
        _logger = logger;
        _options = options.Value;
        _ingest = ingest;
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
