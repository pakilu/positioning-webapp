using System.Text;
using System.Text.Json;
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

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private IManagedMqttClient? _client;

    public MqttIngestService(
        ILogger<MqttIngestService> logger,
        IOptions<MqttOptions> options,
        IHubContext<PositioningHub> hub,
        IServiceScopeFactory scopeFactory)
    {
        _logger = logger;
        _options = options.Value;
        _hub = hub;
        _scopeFactory = scopeFactory;
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
        });

        await _client.StartAsync(managedOptions);

        _logger.LogInformation(
            "MQTT ingest service started. Subscribed to '{RawTopic}' and '{PosTopic}'.",
            _options.RawMeasurementTopic, _options.PositionResultTopic);
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
            if (TopicMatches(topic, _options.PositionResultTopic))
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

        var tagId = await db.Chips
            .Where(c => c.DeviceIdentifier == msg.TagDeviceId)
            .Select(c => (Guid?)c.Id)
            .FirstOrDefaultAsync();
        if (tagId is null)
        {
            _logger.LogWarning("Unknown tag device {DeviceId}; skipping persistence", msg.TagDeviceId);
            return;
        }

        db.Set<PositionResult>().Add(new PositionResult
        {
            SessionId  = msg.SessionId.Value,
            TagChipId  = tagId.Value,
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

        await _hub.Clients.All.SendAsync("RawMeasurement", msg);
        if (msg.SessionId is Guid sid)
        {
            await _hub.Clients.Group(PositioningHub.GroupName(sid))
                .SendAsync("RawMeasurement", msg);
        }

        if (!_options.PersistToDatabase || msg.SessionId is null
            || string.IsNullOrEmpty(msg.TagDeviceId) || string.IsNullOrEmpty(msg.AnchorDeviceId))
            return;

        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var chips = await db.Chips
            .Where(c => c.DeviceIdentifier == msg.TagDeviceId || c.DeviceIdentifier == msg.AnchorDeviceId)
            .Select(c => new { c.Id, c.DeviceIdentifier })
            .ToListAsync();

        var tag    = chips.FirstOrDefault(c => c.DeviceIdentifier == msg.TagDeviceId);
        var anchor = chips.FirstOrDefault(c => c.DeviceIdentifier == msg.AnchorDeviceId);
        if (tag is null || anchor is null)
        {
            _logger.LogWarning("Unknown chip(s) tag={Tag} anchor={Anchor}; skipping persistence",
                msg.TagDeviceId, msg.AnchorDeviceId);
            return;
        }

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
