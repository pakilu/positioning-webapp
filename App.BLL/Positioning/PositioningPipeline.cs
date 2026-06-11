using System.Collections.Concurrent;
using App.DAL.EF;
using App.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace App.BLL.Positioning;

/// <summary>
/// Orchestrates buffer → anchor lookup → solve → persist → publish.
/// Lifetime: singleton. Stateless apart from a small per-tag throttle dictionary.
///
/// Persistence and broadcasting both happen here, but only after the solver
/// succeeds. The MQTT layer remains responsible for persisting raw measurements
/// and broadcasting the raw stream; this class only deals with computed fixes.
/// </summary>
public sealed class PositioningPipeline : IPositioningPipeline
{
    private readonly IMeasurementBuffer _buffer;
    private readonly IAnchorPositionProvider _anchorProvider;
    private readonly ITrilaterationSolver _solver;
    private readonly IPositionResultPublisher _publisher;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly PositioningPipelineOptions _opts;
    private readonly TimeProvider _clock;
    private readonly ILogger<PositioningPipeline> _log;

    /// <summary>Last-solve timestamp per (session, tag) — used for throttling.</summary>
    private readonly ConcurrentDictionary<(Guid Session, Guid Tag), DateTime> _lastSolveAt = new();

    public PositioningPipeline(
        IMeasurementBuffer buffer,
        IAnchorPositionProvider anchorProvider,
        ITrilaterationSolver solver,
        IPositionResultPublisher publisher,
        IServiceScopeFactory scopeFactory,
        PositioningPipelineOptions opts,
        TimeProvider clock,
        ILogger<PositioningPipeline> log)
    {
        _buffer = buffer;
        _anchorProvider = anchorProvider;
        _solver = solver;
        _publisher = publisher;
        _scopeFactory = scopeFactory;
        _opts = opts;
        _clock = clock;
        _log = log;
    }

    public async Task<PositionResultBroadcast?> OnRawMeasurementAsync(
        Guid sessionId, Guid tagId, Guid anchorId,
        double distance, DateTime recordedAt, CancellationToken ct = default)
    {
        // 1. Always record the measurement, regardless of whether we end up solving.
        _buffer.Add(sessionId, tagId, anchorId, distance, recordedAt);

        // 2. Throttle: avoid re-solving on every single anchor message in a round.
        var now = _clock.GetUtcNow().UtcDateTime;
        var key = (sessionId, tagId);
        if (_opts.MinSolveInterval > TimeSpan.Zero
            && _lastSolveAt.TryGetValue(key, out var last)
            && now - last < _opts.MinSolveInterval)
        {
            return null;
        }

        // 3. Take a snapshot of fresh anchor distances for this tag.
        var snapshot = _buffer.Snapshot(sessionId, tagId, _opts.MaxMeasurementAge, now);
        int minAnchors = _opts.Solver.Mode == SolverMode.ThreeD ? 4 : 3;
        if (snapshot.Count < minAnchors) return null;

        // 4. Resolve anchor coordinates (cached).
        var anchorPositions = await _anchorProvider.GetAnchorsAsync(sessionId, ct);
        if (anchorPositions.Count == 0)
        {
            _log.LogDebug("No anchors configured for session {Session}; skipping solve", sessionId);
            return null;
        }

        // 5. Join: drop any buffered measurement whose anchor isn't in this
        //    session's config (could happen if a stray chip publishes on a
        //    topic the broker hasn't filtered).
        var measurements = new List<AnchorMeasurement>(snapshot.Count);
        foreach (var s in snapshot)
        {
            if (anchorPositions.TryGetValue(s.AnchorId, out var p))
                measurements.Add(new AnchorMeasurement(p.X, p.Y, p.Z, s.Distance));
        }
        if (measurements.Count < minAnchors) return null;

        // 6. Solve. The solver is pure; failure means degenerate geometry
        //    (e.g. all anchors collinear) — we just skip this round.
        if (!_solver.TrySolve(measurements, _opts.Solver, out var fix))
        {
            _log.LogDebug(
                "Trilateration failed for session {Session} tag {Tag} with {N} anchors",
                sessionId, tagId, measurements.Count);
            return null;
        }

        _lastSolveAt[key] = now;

        // RecordedAt for the fix = the most recent input timestamp in the snapshot.
        // (More physically meaningful than "now": it's when the last leg of the
        // ranging round actually happened.)
        var fixRecordedAt = MaxRecordedAt(snapshot);

        var broadcast = new PositionResultBroadcast(
            SessionId:   sessionId,
            TagId:       tagId,
            RecordedAt:  fixRecordedAt,
            X:           fix.X,
            Y:           fix.Y,
            Z:           fix.Z,
            Accuracy:    fix.Residual,
            AnchorCount: fix.AnchorCount);

        // 7. Persist + publish. Persist first so a subscriber that re-queries
        //    after receiving the broadcast can see the row.
        if (_opts.PersistResults)
            await PersistAsync(broadcast, ct);

        try
        {
            await _publisher.PublishAsync(broadcast, ct);
        }
        catch (Exception ex)
        {
            // A publisher failure must not break the ingest loop.
            _log.LogWarning(ex, "Position publish failed for session {Session}", sessionId);
        }

        return broadcast;
    }

    private async Task PersistAsync(PositionResultBroadcast b, CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        db.PositionResults.Add(new PositionResult
        {
            SessionId  = b.SessionId,
            TagChipId  = b.TagId,
            RecordedAt = b.RecordedAt,
            XCoord     = (decimal)b.X,
            YCoord     = (decimal)b.Y,
            ZCoord     = (decimal)b.Z,
            Accuracy   = (decimal)b.Accuracy,
        });
        await db.SaveChangesAsync(ct);
    }

    private static DateTime MaxRecordedAt(IReadOnlyList<BufferedDistance> snapshot)
    {
        var max = snapshot[0].RecordedAt;
        for (int i = 1; i < snapshot.Count; i++)
            if (snapshot[i].RecordedAt > max) max = snapshot[i].RecordedAt;
        return max;
    }
}
