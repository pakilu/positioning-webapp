using System.Text;
using System.Text.Json;
using App.BLL.Positioning;
using App.DAL.EF;
using App.Domain;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using MQTTnet;
using MQTTnet.Client;
using MQTTnet.Extensions.ManagedClient;
using WebApp.Hubs;
using WebApp.Models.Mqtt;

namespace WebApp.Services;

/// <summary>
/// Background service that:
///  1. Maintains a managed MQTT connection to Mosquitto (auto-reconnects).
///  2. Subscribes to the raw-measurement and position-result topics.
///  3. Deserializes JSON payloads, optionally persists them to the DB,
///     and broadcasts them to SignalR clients on <see cref="PositioningHub"/>.
/// </summary>
public class MqttIngestService : IHostedService, IAsyncDisposable
{
    private readonly ILogger<MqttIngestService> _logger;
    private readonly MqttOptions _options;
    private readonly IHubContext<PositioningHub> _hub;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IPositioningPipeline _pipeline;

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private IManagedMqttClient? _client;

    public MqttIngestService(
        ILogger<MqttIngestService> logger,
        IOptions<MqttOptions> options,
        IHubContext<PositioningHub> hub,
        IServiceScopeFactory scopeFactory,
        IPositioningPipeline pipeline)
    {
        _logger = logger;
        _options = options.Value;
        _hub = hub;
        _scopeFactory = scopeFactory;
        _pipeline = pipeline;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var clientOptionsBuilder = new MqttClientOptionsBuilder()
            .WithClientId(_options.ClientId)
            .WithTcpServer(_options.Host, _options.Port)
            .WithCleanSession();

        if (!string.IsNullOrEmpty(_options.Username))
        {
            clientOptionsBuilder = clientOptionsBuilder
                .WithCredentials(_options.Username, _options.Password);
        }

        if (_options.UseTls)
        {
            clientOptionsBuilder = clientOptionsBuilder.WithTlsOptions(o => { });
        }

        var managedOptions = new ManagedMqttClientOptionsBuilder()
            .WithAutoReconnectDelay(TimeSpan.FromSeconds(5))
            .WithClientOptions(clientOptionsBuilder.Build())
            .Build();

        _client = new MqttFactory().CreateManagedMqttClient();
        _client.ApplicationMessageReceivedAsync += OnMessageReceivedAsync;
        _client.ConnectedAsync += _ =>
        {
            _logger.LogInformation("Connected to MQTT broker {Host}:{Port}", _options.Host, _options.Port);
            return Task.CompletedTask;
        };
        _client.DisconnectedAsync += e =>
        {
            _logger.LogWarning(e.Exception, "Disconnected from MQTT broker: {Reason}", e.Reason);
            return Task.CompletedTask;
        };

        await _client.SubscribeAsync(new[]
        {
            new MqttTopicFilterBuilder().WithTopic(_options.RawMeasurementTopic).Build(),
            new MqttTopicFilterBuilder().WithTopic(_options.PositionResultTopic).Build(),
            new MqttTopicFilterBuilder().WithTopic(_options.ChipRegistrationTopic).Build(),
        });

        await _client.StartAsync(managedOptions);

        _logger.LogInformation(
            "MQTT ingest service started. Subscribed to '{RawTopic}', '{PosTopic}' and '{RegTopic}'.",
            _options.RawMeasurementTopic, _options.PositionResultTopic, _options.ChipRegistrationTopic);
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_client is not null)
        {
            await _client.StopAsync();
        }
    }

    private async Task OnMessageReceivedAsync(MqttApplicationMessageReceivedEventArgs e)
    {
        var topic = e.ApplicationMessage.Topic;
        var payload = e.ApplicationMessage.PayloadSegment.Array is null
            ? string.Empty
            : Encoding.UTF8.GetString(e.ApplicationMessage.PayloadSegment);

        _logger.LogDebug("MQTT [{Topic}] {Payload}", topic, payload);

        try
        {
            if (TopicMatches(topic, _options.ChipRegistrationTopic))
            {
                await HandleRegistrationAsync(payload);
            }
            else if (TopicMatches(topic, _options.PositionResultTopic))
            {
                var msg = JsonSerializer.Deserialize<PositionResultMessage>(payload, JsonOpts);
                if (msg is not null) await HandlePositionAsync(msg);
            }
            else if (TopicMatches(topic, _options.RawMeasurementTopic))
            {
                var msg = JsonSerializer.Deserialize<RawMeasurementMessage>(payload, JsonOpts);
                if (msg is not null) await HandleRawAsync(msg);
            }
            else
            {
                _logger.LogDebug("Ignoring MQTT message on unmatched topic {Topic}", topic);
            }
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Malformed JSON on topic {Topic}: {Payload}", topic, payload);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to handle MQTT message on topic {Topic}", topic);
        }
    }

    /// <summary>
    /// Handles chip-registration announcements. Accepts either a plain MAC
    /// string payload (e.g. <c>"AA:BB:CC:11:22:33"</c>) or a small JSON
    /// object <c>{"mac":"..."}</c>. If the MAC isn't already known, a new
    /// <see cref="Chip"/> row is inserted with the MAC as both
    /// DeviceIdentifier and Name. The operator can rename it later.
    /// </summary>
    private async Task HandleRegistrationAsync(string payload)
    {
        var mac = ExtractMac(payload);
        if (string.IsNullOrWhiteSpace(mac))
        {
            _logger.LogWarning("Empty/invalid registration payload: {Payload}", payload);
            return;
        }

        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await GetOrCreateChipAsync(db, mac);
    }

    private static string? ExtractMac(string payload)
    {
        if (string.IsNullOrWhiteSpace(payload)) return null;
        var trimmed = payload.Trim();

        // JSON form: { "mac": "..." }
        if (trimmed.StartsWith("{"))
        {
            try
            {
                using var doc = JsonDocument.Parse(trimmed);
                if (doc.RootElement.TryGetProperty("mac", out var macProp) &&
                    macProp.ValueKind == JsonValueKind.String)
                {
                    return macProp.GetString()?.Trim();
                }
            }
            catch (JsonException) { /* fall through */ }
            return null;
        }

        // Plain MAC string. Strip optional surrounding quotes.
        return trimmed.Trim('"');
    }

    /// <summary>
    /// Looks up a chip by MAC, inserting a new row with the MAC as both
    /// DeviceIdentifier and Name if none exists yet.
    /// </summary>
    private async Task<Chip> GetOrCreateChipAsync(AppDbContext db, string deviceId)
    {
        var chip = await db.Chips.FirstOrDefaultAsync(c => c.DeviceIdentifier == deviceId);
        if (chip is not null) return chip;

        chip = new Chip
        {
            DeviceIdentifier = deviceId,
            Name = deviceId,
        };
        db.Chips.Add(chip);
        try
        {
            await db.SaveChangesAsync();
            _logger.LogInformation("Registered new chip {DeviceId}", deviceId);
        }
        catch (DbUpdateException)
        {
            // Race: another message inserted the same MAC concurrently.
            db.Entry(chip).State = EntityState.Detached;
            chip = await db.Chips.FirstAsync(c => c.DeviceIdentifier == deviceId);
        }
        return chip;
    }

    private async Task HandlePositionAsync(PositionResultMessage msg)
    {
        msg.RecordedAt ??= DateTime.UtcNow;

        // 1) Broadcast to all clients
        await _hub.Clients.All.SendAsync("PositionResult", msg);

        // 2) Broadcast to per-session group (if a session is provided)
        if (msg.SessionId is Guid sid)
        {
            await _hub.Clients.Group(PositioningHub.GroupName(sid))
                .SendAsync("PositionResult", msg);
        }

        // 3) Optional DB persistence
        if (!_options.PersistToDatabase || msg.SessionId is null || string.IsNullOrEmpty(msg.TagDeviceId))
            return;

        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var tag = await GetOrCreateChipAsync(db, msg.TagDeviceId);

        db.Set<PositionResult>().Add(new PositionResult
        {
            SessionId  = msg.SessionId.Value,
            TagChipId  = tag.Id,
            RecordedAt = msg.RecordedAt.Value,
            XCoord     = msg.XCoord,
            YCoord     = msg.YCoord,
            ZCoord     = msg.ZCoord,
            Accuracy   = msg.Accuracy,
        });
        await db.SaveChangesAsync();
    }

    private async Task HandleRawAsync(RawMeasurementMessage msg)
    {
        msg.RecordedAt ??= DateTime.UtcNow;

        // Always broadcast the raw stream to listeners.
        await _hub.Clients.All.SendAsync("RawMeasurement", msg);
        if (msg.SessionId is Guid sid)
        {
            await _hub.Clients.Group(PositioningHub.GroupName(sid))
                .SendAsync("RawMeasurement", msg);
        }

        // Need a session, both device identifiers, and a distance to do
        // anything further (chip resolution, persistence, trilateration).
        if (msg.SessionId is null
            || string.IsNullOrEmpty(msg.TagDeviceId)
            || string.IsNullOrEmpty(msg.AnchorDeviceId)
            || msg.Distance is null)
            return;

        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var tag    = await GetOrCreateChipAsync(db, msg.TagDeviceId);
        var anchor = await GetOrCreateChipAsync(db, msg.AnchorDeviceId);

        // Persist the raw measurement (gated by the existing config flag).
        if (_options.PersistToDatabase)
        {
            db.Set<RawMeasurement>().Add(new RawMeasurement
            {
                SessionId     = msg.SessionId.Value,
                TagChipId     = tag.Id,
                AnchorChipId  = anchor.Id,
                RecordedAt    = msg.RecordedAt.Value,
                Distance      = msg.Distance,
                Rssi          = msg.Rssi,
                Snr           = msg.Snr,
                Quality       = msg.Quality,
            });
            await db.SaveChangesAsync();
        }

        // Feed the positioning pipeline. It buffers, decides when to solve,
        // persists the PositionResult, and broadcasts via SignalR.
        await _pipeline.OnRawMeasurementAsync(
            sessionId:  msg.SessionId.Value,
            tagId:      tag.Id,
            anchorId:   anchor.Id,
            distance:   (double)msg.Distance.Value,
            recordedAt: msg.RecordedAt.Value);
    }

    /// <summary>Very small MQTT topic-filter matcher (supports + and #).</summary>
    private static bool TopicMatches(string topic, string filter)
    {
        var tParts = topic.Split('/');
        var fParts = filter.Split('/');

        for (int i = 0; i < fParts.Length; i++)
        {
            if (fParts[i] == "#") return true;
            if (i >= tParts.Length) return false;
            if (fParts[i] == "+") continue;
            if (fParts[i] != tParts[i]) return false;
        }
        return tParts.Length == fParts.Length;
    }

    public async ValueTask DisposeAsync()
    {
        if (_client is not null)
        {
            await _client.StopAsync();
            _client.Dispose();
        }
        GC.SuppressFinalize(this);
    }
}
