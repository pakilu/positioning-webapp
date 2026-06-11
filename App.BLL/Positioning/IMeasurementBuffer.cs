namespace App.BLL.Positioning;

/// <summary>One anchor's contribution to a tag's current ranging snapshot.</summary>
/// <param name="AnchorId">Chip Id of the anchor.</param>
/// <param name="Distance">Latest measured distance (m).</param>
/// <param name="RecordedAt">When the measurement was taken (UTC).</param>
public readonly record struct BufferedDistance(Guid AnchorId, double Distance, DateTime RecordedAt);

/// <summary>
/// In-memory rolling cache of the most recent anchor distance per
/// (session, tag, anchor). The trilateration pipeline writes to it as MQTT
/// messages arrive and reads a fresh snapshot before each solve.
///
/// Why "latest per anchor" rather than a queue: UWB tags poll anchors in
/// roughly periodic rounds. Keeping only the latest sample naturally implements
/// "solve with the most recent ranging round" without round-boundary tracking.
/// </summary>
public interface IMeasurementBuffer
{
    /// <summary>
    /// Record (or overwrite) the latest distance from <paramref name="anchorId"/>
    /// to <paramref name="tagId"/> in session <paramref name="sessionId"/>.
    /// Thread-safe.
    /// </summary>
    void Add(Guid sessionId, Guid tagId, Guid anchorId, double distance, DateTime recordedAt);

    /// <summary>
    /// Return the currently-fresh anchor distances for this tag.
    /// Entries older than <paramref name="maxAge"/> (relative to <paramref name="now"/>)
    /// are excluded — but not removed; <see cref="Prune"/> handles eviction.
    /// </summary>
    IReadOnlyList<BufferedDistance> Snapshot(
        Guid sessionId, Guid tagId, TimeSpan maxAge, DateTime now);

    /// <summary>
    /// Remove all entries older than <paramref name="maxAge"/> across every
    /// (session, tag). Cheap to call periodically (e.g. once a second) to keep
    /// memory bounded when tags or sessions go away.
    /// </summary>
    void Prune(TimeSpan maxAge, DateTime now);

    /// <summary>Drop all buffered data for a session (e.g. when it ends).</summary>
    void ClearSession(Guid sessionId);
}
