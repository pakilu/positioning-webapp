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
            new MqttTopicFilterBuilder().WithTopic(_options.ChipRegistrationTopic).Build(),
        });

        await _client.StartAsync(managedOptions);

        _logger.LogInformation(
            "MQTT ingest service started. Subscribed to '{RawTopic}' and '{RegTopic}'.",
            _options.RawMeasurementTopic, _options.ChipRegistrationTopic);
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
    /// Handles chip-registration announcements. Expected JSON shape:
    /// <c>{"deviceIdentifier":"0x01","macAddress":"AA:BB:CC:11:22:33"}</c>.
    /// The short <c>deviceIdentifier</c> is stored as <see cref="Chip.DeviceIdentifier"/>
    /// (this is the value carried in the DW3000 over-the-air frames and in
    /// raw-measurement MQTT messages, so it's the join key used by the
    /// positioning pipeline). The MAC, when present, is used as the chip's
    /// default <see cref="Chip.Name"/> so the operator can recognise the
    /// hardware in the admin UI. Both are also accepted under the legacy
    /// field names <c>tagDeviceId</c> / <c>mac</c>, and a bare MAC string
    /// payload is still tolerated for backwards compatibility (in which case
    /// the MAC is used as the DeviceIdentifier, matching the old behaviour).
    /// </summary>
    private async Task HandleRegistrationAsync(string payload)
    {
        var reg = ParseRegistration(payload);
        if (reg is null || string.IsNullOrWhiteSpace(reg.Value.DeviceIdentifier))
        {
            _logger.LogWarning("Empty/invalid registration payload: {Payload}", payload);
            return;
        }

        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await RegisterChipAsync(db, reg.Value.DeviceIdentifier!, reg.Value.MacAddress);
    }

    private readonly record struct RegistrationInfo(string? DeviceIdentifier, string? MacAddress);

    private static RegistrationInfo? ParseRegistration(string payload)
    {
        if (string.IsNullOrWhiteSpace(payload)) return null;
        var trimmed = payload.Trim();

        // JSON form
        if (trimmed.StartsWith("{"))
        {
            try
            {
                using var doc = JsonDocument.Parse(trimmed);
                var root = doc.RootElement;

                string? deviceId = null;
                foreach (var name in new[] { "deviceIdentifier", "tagDeviceId", "anchorDeviceId" })
                {
                    if (root.TryGetProperty(name, out var p) && p.ValueKind == JsonValueKind.String)
                    {
                        deviceId = p.GetString()?.Trim();
                        if (!string.IsNullOrEmpty(deviceId)) break;
                    }
                }

                string? mac = null;
                foreach (var name in new[] { "macAddress", "mac" })
                {
                    if (root.TryGetProperty(name, out var p) && p.ValueKind == JsonValueKind.String)
                    {
                        mac = p.GetString()?.Trim();
                        if (!string.IsNullOrEmpty(mac)) break;
                    }
                }

                // Legacy: if no explicit deviceIdentifier was sent but a MAC was,
                // fall back to using the MAC as the identifier (old behaviour).
                if (string.IsNullOrEmpty(deviceId)) deviceId = mac;

                return new RegistrationInfo(deviceId, mac);
            }
            catch (JsonException) { return null; }
        }

        // Plain MAC string payload (legacy). Strip optional surrounding quotes.
        var bare = trimmed.Trim('"');
        return new RegistrationInfo(bare, bare);
    }

    /// <summary>
    /// Looks up a chip by its short <paramref name="deviceIdentifier"/> and
    /// inserts a new row if none exists. When the chip is being created and a
    /// <paramref name="macAddress"/> is supplied, the MAC is used as the
    /// initial <see cref="Chip.Name"/>; otherwise the device identifier is
    /// used. Existing chips are left untouched so the operator's manual
    /// renaming is preserved across re-registrations.
    /// </summary>
    private async Task<Chip> RegisterChipAsync(AppDbContext db, string deviceIdentifier, string? macAddress)
    {
        var chip = await db.Chips.FirstOrDefaultAsync(c => c.DeviceIdentifier == deviceIdentifier);
        if (chip is not null) return chip;

        var name = !string.IsNullOrWhiteSpace(macAddress) ? macAddress! : deviceIdentifier;

        chip = new Chip
        {
            DeviceIdentifier = deviceIdentifier,
            Name = name,
        };
        db.Chips.Add(chip);
        try
        {
            await db.SaveChangesAsync();
            _logger.LogInformation(
                "Registered new chip DeviceIdentifier='{DeviceId}' Name='{Name}'",
                deviceIdentifier, name);
        }
        catch (DbUpdateException)
        {
            // Race: another message inserted the same identifier concurrently.
            db.Entry(chip).State = EntityState.Detached;
            chip = await db.Chips.FirstAsync(c => c.DeviceIdentifier == deviceIdentifier);
        }
        return chip;
    }

    private async Task HandleRawAsync(RawMeasurementMessage msg)
    {
        msg.RecordedAt ??= DateTime.UtcNow;

        // Need a session, both device identifiers, and a distance to do
        // anything further (chip resolution, persistence, trilateration).
        if (string.IsNullOrEmpty(msg.TagDeviceId)
            || string.IsNullOrEmpty(msg.AnchorDeviceId)
            || msg.Distance is null)
        {
            await BroadcastRawAsync(msg);
            return;
        }

        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var tag    = await db.Chips.FirstOrDefaultAsync(c => c.DeviceIdentifier == msg.TagDeviceId);
        var anchor = await db.Chips.FirstOrDefaultAsync(c => c.DeviceIdentifier == msg.AnchorDeviceId);

        if (tag is null || anchor is null)
        {
            _logger.LogWarning(
                "Raw measurement references unknown chip(s): tag DeviceIdentifier='{TagDeviceId}' (found={TagFound}), anchor DeviceIdentifier='{AnchorDeviceId}' (found={AnchorFound}). " +
                "Chips must be registered via the chip-registration topic before they can be used. Skipping.",
                msg.TagDeviceId, tag is not null,
                msg.AnchorDeviceId, anchor is not null);
            await BroadcastRawAsync(msg);
            return;
        }

        if (msg.SessionId is null)
        {
            msg.SessionId = await TryResolveActiveSessionAsync(db, tag.Id, anchor.Id);
        }

        await BroadcastRawAsync(msg);

        if (msg.SessionId is null)
        {
            _logger.LogDebug(
                "Raw measurement from tag {TagDeviceId} and anchor {AnchorDeviceId} has no active matching session; skipping solve.",
                msg.TagDeviceId,
                msg.AnchorDeviceId);
            return;
        }

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

    private async Task BroadcastRawAsync(RawMeasurementMessage msg)
    {
        await _hub.Clients.All.SendAsync("RawMeasurement", msg);
        if (msg.SessionId is Guid sid)
        {
            await _hub.Clients.Group(PositioningHub.GroupName(sid))
                .SendAsync("RawMeasurement", msg);
        }
    }

    private static async Task<Guid?> TryResolveActiveSessionAsync(AppDbContext db, Guid tagId, Guid anchorId)
    {
        var matches = await db.Sessions
            .Where(s => s.Status == ESessionStatus.Active)
            .Where(s => s.SessionConfig.SessionConfigChips.Any(c =>
                c.ChipId == tagId && c.Role == EChipRole.Tag))
            .Where(s => s.SessionConfig.SessionConfigChips.Any(c =>
                c.ChipId == anchorId && c.Role == EChipRole.Anchor))
            .OrderByDescending(s => s.StartedAt ?? s.CreatedAt)
            .Select(s => s.Id)
            .Take(2)
            .ToListAsync();

        return matches.Count == 1 ? matches[0] : null;
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
