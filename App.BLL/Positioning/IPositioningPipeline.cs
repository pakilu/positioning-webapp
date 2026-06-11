namespace App.BLL.Positioning;

/// <summary>
/// Tuning knobs for <see cref="PositioningPipeline"/>.
/// </summary>
public sealed class PositioningPipelineOptions
{
    /// <summary>
    /// Measurements older than this are ignored when forming a snapshot.
    /// Should be ~1–2 ranging rounds. Default 500 ms.
    /// </summary>
    public TimeSpan MaxMeasurementAge { get; set; } = TimeSpan.FromMilliseconds(500);

    /// <summary>
    /// Minimum time between two solves for the same (session, tag). Prevents
    /// flooding SignalR clients when each ranging round produces N back-to-back
    /// MQTT messages. Default 80 ms ≈ 12 Hz fix rate. Set to <see cref="TimeSpan.Zero"/>
    /// to solve on every incoming message.
    /// </summary>
    public TimeSpan MinSolveInterval { get; set; } = TimeSpan.FromMilliseconds(80);

    /// <summary>
    /// If true (default), every successful fix is written to <c>PositionResults</c>.
    /// Set to false to broadcast over the publisher without DB writes.
    /// </summary>
    public bool PersistResults { get; set; } = true;

    /// <summary>Trilateration solver options. Defaults to 2D with mean anchor Z.</summary>
    public TrilaterationOptions Solver { get; set; } = new();
}

/// <summary>
/// Glue between the MQTT ingest path and the trilateration solver.
/// Call <see cref="OnRawMeasurementAsync"/> once per incoming distance sample;
/// the pipeline buffers, decides when to solve, persists, and broadcasts.
/// </summary>
public interface IPositioningPipeline
{
    /// <summary>
    /// Feed one anchor → tag distance into the pipeline. Safe to call from any thread.
    /// Returns the produced fix when a solve happened this call, otherwise <c>null</c>
    /// (insufficient anchors, throttled, or solver failure).
    /// </summary>
    Task<PositionResultBroadcast?> OnRawMeasurementAsync(
        Guid sessionId,
        Guid tagId,
        Guid anchorId,
        double distance,
        DateTime recordedAt,
        CancellationToken ct = default);
}
