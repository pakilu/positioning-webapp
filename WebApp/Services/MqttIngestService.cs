using System.Text;
using System.Text.Json;
using App.DAL.EF;
using App.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using MQTTnet;
using MQTTnet.Client;
using MQTTnet.Extensions.ManagedClient;
using MQTTnet.Protocol;
using WebApp.Models.Mqtt;

namespace WebApp.Services;

/// <summary>
/// Background service that:
///  1. Maintains a managed MQTT connection to Mosquitto (auto-reconnects).
///  2. Subscribes to the raw-measurement and position-result topics.
///  3. Deserializes JSON payloads, optionally persists them to the DB,
///     and broadcasts them to SignalR clients on <see cref="PositioningHub"/>.
/// </summary>
public class MqttIngestService : IHostedService, IAnchorListPublisher, IAsyncDisposable
{
    private readonly ILogger<MqttIngestService> _logger;
    private readonly MqttOptions _options;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IngestProcessor _ingest;

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private IManagedMqttClient? _client;

    public MqttIngestService(
        ILogger<MqttIngestService> logger,
        IOptions<MqttOptions> options,
        IServiceScopeFactory scopeFactory,
        IngestProcessor ingest)
    {
        _logger = logger;
        _options = options.Value;
        _scopeFactory = scopeFactory;
        _ingest = ingest;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (!_options.Enabled)
        {
            _logger.LogInformation("MQTT ingest service is disabled.");
            return;
        }

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

    // ------------------------------------------------------------------
    // IAnchorListPublisher
    // ------------------------------------------------------------------

    public async Task PublishAnchorListAsync(
        string tagDeviceIdentifier,
        IEnumerable<string> anchorDeviceIdentifiers,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(tagDeviceIdentifier))
        {
            _logger.LogWarning("PublishAnchorListAsync called with empty tag DeviceIdentifier; skipping.");
            return;
        }

        if (_client is null)
        {
            _logger.LogWarning("MQTT client not initialized yet; cannot publish anchor list for tag {TagId}.", tagDeviceIdentifier);
            return;
        }

        // Dedupe + drop empties while preserving order.
        var anchors = anchorDeviceIdentifiers
            .Where(a => !string.IsNullOrWhiteSpace(a))
            .Select(a => a.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var topic = BuildAnchorListTopic(tagDeviceIdentifier);
        var payload = JsonSerializer.Serialize(anchors);

        var message = new MqttApplicationMessageBuilder()
            .WithTopic(topic)
            .WithPayload(payload)
            .WithRetainFlag(true)
            .WithQualityOfServiceLevel(MqttQualityOfServiceLevel.AtLeastOnce)
            .Build();

        await _client.EnqueueAsync(message);

        _logger.LogInformation(
            "Published anchor list to '{Topic}' (retained, {Count} anchors): {Payload}",
            topic, anchors.Length, payload);
    }

    public async Task PublishForSessionAsync(Guid sessionId, bool clear = false, CancellationToken cancellationToken = default)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var session = await db.Sessions
            .Include(s => s.SessionConfig)
                .ThenInclude(sc => sc.SessionConfigChips)
                    .ThenInclude(scc => scc.Chip)
            .FirstOrDefaultAsync(s => s.Id == sessionId, cancellationToken);

        if (session is null)
        {
            _logger.LogWarning("PublishForSessionAsync: session {SessionId} not found.", sessionId);
            return;
        }

        var chips = session.SessionConfig.SessionConfigChips;

        var tagIds = chips
            .Where(c => c.Role == EChipRole.Tag && c.Chip != null && !string.IsNullOrWhiteSpace(c.Chip.DeviceIdentifier))
            .Select(c => c.Chip.DeviceIdentifier!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var anchorIds = clear
            ? Array.Empty<string>()
            : chips
                .Where(c => c.Role == EChipRole.Anchor && c.Chip != null && !string.IsNullOrWhiteSpace(c.Chip.DeviceIdentifier))
                .Select(c => c.Chip.DeviceIdentifier!)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();

        foreach (var tag in tagIds)
        {
            await PublishAnchorListAsync(tag, anchorIds, cancellationToken);
        }
    }

    private string BuildAnchorListTopic(string tagDeviceIdentifier)
    {
        return _options.AnchorListTopicTemplate.Replace("{tagDeviceId}", tagDeviceIdentifier);
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
                await _ingest.HandleRegistrationAsync(payload);
            }
            else if (TopicMatches(topic, _options.RawMeasurementTopic))
            {
                var msg = JsonSerializer.Deserialize<RawMeasurementMessage>(payload, JsonOpts);
                if (msg is not null) await _ingest.HandleRawAsync(msg, _options.PersistToDatabase);
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
