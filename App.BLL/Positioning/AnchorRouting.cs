using App.DAL.EF;
using App.Domain;
using Microsoft.EntityFrameworkCore;

namespace App.BLL.Positioning;

public interface IAnchorRoutingService
{
    Task<AnchorRoutingDecision?> GetNextAnchorsAsync(
        Guid sessionId,
        string tagDeviceIdentifier,
        int desiredCount,
        CancellationToken ct = default);
}

public sealed record AnchorRoutingDecision(
    Guid SessionId,
    Guid TagId,
    string TagDeviceIdentifier,
    bool PositionKnown,
    bool MovementKnown,
    PositionVector? Position,
    PositionVector? Velocity,
    IReadOnlyList<AnchorRoutingItem> Anchors);

public sealed record AnchorRoutingItem(
    Guid ChipId,
    string DeviceIdentifier,
    string Name,
    double X,
    double Y,
    double Z);

public sealed record PositionVector(double X, double Y, double Z);

public sealed class AnchorRoutingService : IAnchorRoutingService
{
    public const int DefaultAnchorCount = 4;
    public const int MinimumAnchorCount = 3;

    private readonly AppDbContext _db;

    public AnchorRoutingService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<AnchorRoutingDecision?> GetNextAnchorsAsync(
        Guid sessionId,
        string tagDeviceIdentifier,
        int desiredCount,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(tagDeviceIdentifier))
            return null;

        var count = Math.Max(MinimumAnchorCount, desiredCount <= 0 ? DefaultAnchorCount : desiredCount);

        var tag = await _db.Chips
            .Where(c => c.DeviceIdentifier == tagDeviceIdentifier)
            .Select(c => new { c.Id, c.DeviceIdentifier })
            .FirstOrDefaultAsync(ct);
        if (tag is null)
            return null;

        var anchors = await _db.Sessions
            .Where(s => s.Id == sessionId)
            .SelectMany(s => s.SessionConfig.SessionConfigChips)
            .Where(c => c.Role == EChipRole.Anchor && c.XCoord != null && c.YCoord != null)
            .Select(c => new AnchorRoutingItem(
                c.ChipId,
                c.Chip.DeviceIdentifier,
                c.Chip.Name,
                (double)c.XCoord!.Value,
                (double)c.YCoord!.Value,
                (double)(c.ZCoord ?? 0m)))
            .ToListAsync(ct);

        if (anchors.Count == 0)
            return null;

        count = Math.Min(count, anchors.Count);

        var fixes = await _db.PositionResults
            .Where(p => p.SessionId == sessionId && p.TagChipId == tag.Id)
            .OrderByDescending(p => p.RecordedAt)
            .ThenByDescending(p => p.CreatedAt)
            .Select(p => new
            {
                p.RecordedAt,
                X = (double)p.XCoord,
                Y = (double)p.YCoord,
                Z = (double)(p.ZCoord ?? 0m),
            })
            .Take(2)
            .ToListAsync(ct);

        if (fixes.Count == 0)
        {
            var bootstrap = SelectBootstrapAnchors(anchors, count);
            return new AnchorRoutingDecision(
                sessionId,
                tag.Id,
                tag.DeviceIdentifier,
                PositionKnown: false,
                MovementKnown: false,
                Position: null,
                Velocity: null,
                Anchors: bootstrap);
        }

        var latest = fixes[0];
        var position = new PositionVector(latest.X, latest.Y, latest.Z);
        PositionVector? velocity = null;
        if (fixes.Count > 1)
        {
            var previous = fixes[1];
            var dt = (latest.RecordedAt - previous.RecordedAt).TotalSeconds;
            if (dt > 0.05)
            {
                velocity = new PositionVector(
                    (latest.X - previous.X) / dt,
                    (latest.Y - previous.Y) / dt,
                    (latest.Z - previous.Z) / dt);
            }
        }

        return new AnchorRoutingDecision(
            sessionId,
            tag.Id,
            tag.DeviceIdentifier,
            PositionKnown: true,
            MovementKnown: velocity is not null && Speed2D(velocity) >= 0.05,
            Position: position,
            Velocity: velocity,
            Anchors: SelectAnchors(anchors, position, velocity, count));
    }

    private static IReadOnlyList<AnchorRoutingItem> SelectBootstrapAnchors(
        IReadOnlyList<AnchorRoutingItem> anchors,
        int count)
    {
        var center = new PositionVector(anchors.Average(a => a.X), anchors.Average(a => a.Y), anchors.Average(a => a.Z));
        return SelectAnchors(anchors, center, velocity: null, count);
    }

    private static IReadOnlyList<AnchorRoutingItem> SelectAnchors(
        IReadOnlyList<AnchorRoutingItem> anchors,
        PositionVector position,
        PositionVector? velocity,
        int count)
    {
        var selected = new List<AnchorRoutingItem>(count);
        var remaining = anchors.OrderBy(a => Distance2D(a, position)).ThenBy(a => a.DeviceIdentifier).ToList();

        AddBest(remaining, selected, a => -Distance2D(a, position));

        if (velocity is not null && Speed2D(velocity) >= 0.05 && selected.Count < count)
            AddBest(remaining, selected, a => ForwardScore(a, position, velocity));

        while (selected.Count < count && remaining.Count > 0)
        {
            AddBest(remaining, selected, a =>
            {
                var distance = Distance2D(a, position);
                var spread = selected.Count == 0 ? 0.0 : selected.Min(s => AnchorDistance2D(a, s));
                var direction = velocity is null ? 0.0 : ForwardScore(a, position, velocity);
                return spread * 0.65 - distance * 0.35 + direction * 0.45;
            });
        }

        return selected;
    }

    private static void AddBest(
        List<AnchorRoutingItem> remaining,
        List<AnchorRoutingItem> selected,
        Func<AnchorRoutingItem, double> score)
    {
        var best = remaining
            .OrderByDescending(score)
            .ThenBy(a => a.DeviceIdentifier)
            .First();
        remaining.Remove(best);
        selected.Add(best);
    }

    private static double ForwardScore(AnchorRoutingItem anchor, PositionVector position, PositionVector velocity)
    {
        var speed = Speed2D(velocity);
        if (speed < 0.05)
            return 0.0;

        var dx = anchor.X - position.X;
        var dy = anchor.Y - position.Y;
        var dist = Math.Sqrt(dx * dx + dy * dy);
        if (dist < 1e-9)
            return 0.0;

        var alignment = (dx * velocity.X + dy * velocity.Y) / (dist * speed);
        return alignment * 2.0 + Math.Max(0.0, alignment) * dist * 0.15;
    }

    private static double Distance2D(AnchorRoutingItem anchor, PositionVector position)
    {
        var dx = anchor.X - position.X;
        var dy = anchor.Y - position.Y;
        return Math.Sqrt(dx * dx + dy * dy);
    }

    private static double AnchorDistance2D(AnchorRoutingItem a, AnchorRoutingItem b)
    {
        var dx = a.X - b.X;
        var dy = a.Y - b.Y;
        return Math.Sqrt(dx * dx + dy * dy);
    }

    private static double Speed2D(PositionVector velocity)
        => Math.Sqrt(velocity.X * velocity.X + velocity.Y * velocity.Y);
}
