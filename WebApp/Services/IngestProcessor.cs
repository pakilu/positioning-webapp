using System.Text.Json;
using App.BLL.Positioning;
using App.DAL.EF;
using App.Domain;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using WebApp.Hubs;
using WebApp.Models.Mqtt;

namespace WebApp.Services;

public class IngestProcessor
{
    private readonly ILogger<IngestProcessor> _logger;
    private readonly IHubContext<PositioningHub> _hub;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IPositioningPipeline _pipeline;

    public IngestProcessor(
        ILogger<IngestProcessor> logger,
        IHubContext<PositioningHub> hub,
        IServiceScopeFactory scopeFactory,
        IPositioningPipeline pipeline)
    {
        _logger = logger;
        _hub = hub;
        _scopeFactory = scopeFactory;
        _pipeline = pipeline;
    }

    public async Task HandleRegistrationAsync(string payload)
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

    public async Task HandleRawAsync(RawMeasurementMessage msg, bool persistToDatabase)
    {
        msg.RecordedAt ??= DateTime.UtcNow;
        await BroadcastRawAsync(msg);

        if (string.IsNullOrEmpty(msg.TagDeviceId)
            || string.IsNullOrEmpty(msg.AnchorDeviceId)
            || msg.Distance is null)
        {
            return;
        }

        try
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            await HandleRawWithDatabaseAsync(db, msg, persistToDatabase);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Raw measurement was broadcast, but database processing failed for tag {TagDeviceId} and anchor {AnchorDeviceId}.",
                msg.TagDeviceId,
                msg.AnchorDeviceId);
        }
    }

    private async Task HandleRawWithDatabaseAsync(AppDbContext db, RawMeasurementMessage msg, bool persistToDatabase)
    {
        if (msg.RecordedAt is null || msg.Distance is null)
        {
            return;
        }

        var tag = await db.Chips.FirstOrDefaultAsync(c => c.DeviceIdentifier == msg.TagDeviceId);
        var anchor = await db.Chips.FirstOrDefaultAsync(c => c.DeviceIdentifier == msg.AnchorDeviceId);

        if (tag is null || anchor is null)
        {
            _logger.LogWarning(
                "Raw measurement references unknown chip(s): tag DeviceIdentifier='{TagDeviceId}' (found={TagFound}), anchor DeviceIdentifier='{AnchorDeviceId}' (found={AnchorFound}). " +
                "Chips must be registered before they can be used. Skipping.",
                msg.TagDeviceId, tag is not null,
                msg.AnchorDeviceId, anchor is not null);
            return;
        }

        if (msg.SessionId is null)
        {
            msg.SessionId = await TryResolveActiveSessionAsync(db, tag.Id, anchor.Id);
        }

        if (msg.SessionId is null)
        {
            _logger.LogDebug(
                "Raw measurement from tag {TagDeviceId} and anchor {AnchorDeviceId} has no active matching session; skipping solve.",
                msg.TagDeviceId,
                msg.AnchorDeviceId);
            return;
        }

        var sessionId = msg.SessionId.Value;
        var recordedAt = msg.RecordedAt.Value;
        var distance = msg.Distance.Value;

        if (persistToDatabase)
        {
            db.Set<RawMeasurement>().Add(new RawMeasurement
            {
                SessionId = sessionId,
                TagChipId = tag.Id,
                AnchorChipId = anchor.Id,
                RecordedAt = recordedAt,
                Distance = distance,
                Rssi = msg.Rssi,
                Snr = msg.Snr,
                Quality = msg.Quality,
            });
            await db.SaveChangesAsync();
        }

        await _pipeline.OnRawMeasurementAsync(
            sessionId: sessionId,
            tagId: tag.Id,
            anchorId: anchor.Id,
            distance: (double)distance,
            recordedAt: recordedAt);
    }

    private readonly record struct RegistrationInfo(string? DeviceIdentifier, string? MacAddress);

    private static RegistrationInfo? ParseRegistration(string payload)
    {
        if (string.IsNullOrWhiteSpace(payload)) return null;
        var trimmed = payload.Trim();

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

                if (string.IsNullOrEmpty(deviceId)) deviceId = mac;

                return new RegistrationInfo(deviceId, mac);
            }
            catch (JsonException)
            {
                return null;
            }
        }

        var bare = trimmed.Trim('"');
        return new RegistrationInfo(bare, bare);
    }

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
            db.Entry(chip).State = EntityState.Detached;
            chip = await db.Chips.FirstAsync(c => c.DeviceIdentifier == deviceIdentifier);
        }
        return chip;
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
}
