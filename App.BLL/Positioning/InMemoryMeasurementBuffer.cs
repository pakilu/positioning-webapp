using System.Collections.Concurrent;

namespace App.BLL.Positioning;

/// <summary>
/// Thread-safe in-memory implementation of <see cref="IMeasurementBuffer"/>.
///
/// Storage shape:
/// <code>
///   ConcurrentDictionary&lt;(SessionId, TagId), ConcurrentDictionary&lt;AnchorId, Entry&gt;&gt;
/// </code>
/// The outer dict gives O(1) per-tag lookup; the inner gives O(1) per-anchor
/// upsert. Both are <see cref="ConcurrentDictionary{TKey,TValue}"/> because MQTT
/// messages arrive on thread-pool threads with no ordering guarantees.
/// </summary>
public sealed class InMemoryMeasurementBuffer : IMeasurementBuffer
{
    private readonly ConcurrentDictionary<TagKey, ConcurrentDictionary<Guid, Entry>> _data = new();

    private readonly record struct TagKey(Guid SessionId, Guid TagId);
    private readonly record struct Entry(double Distance, DateTime RecordedAt);

    public void Add(Guid sessionId, Guid tagId, Guid anchorId, double distance, DateTime recordedAt)
    {
        var perTag = _data.GetOrAdd(new TagKey(sessionId, tagId),
            _ => new ConcurrentDictionary<Guid, Entry>());

        // Upsert latest measurement, but never overwrite a newer one with an
        // older one (out-of-order MQTT delivery, retries, etc.).
        perTag.AddOrUpdate(
            anchorId,
            _ => new Entry(distance, recordedAt),
            (_, existing) => recordedAt >= existing.RecordedAt
                ? new Entry(distance, recordedAt)
                : existing);
    }

    public IReadOnlyList<BufferedDistance> Snapshot(
        Guid sessionId, Guid tagId, TimeSpan maxAge, DateTime now)
    {
        if (!_data.TryGetValue(new TagKey(sessionId, tagId), out var perTag) || perTag.IsEmpty)
            return Array.Empty<BufferedDistance>();

        var cutoff = now - maxAge;
        var result = new List<BufferedDistance>(perTag.Count);

        // ConcurrentDictionary enumeration is a safe O(n) snapshot.
        foreach (var (anchorId, entry) in perTag)
        {
            if (entry.RecordedAt >= cutoff)
                result.Add(new BufferedDistance(anchorId, entry.Distance, entry.RecordedAt));
        }
        return result;
    }

    public void Prune(TimeSpan maxAge, DateTime now)
    {
        var cutoff = now - maxAge;
        foreach (var (tagKey, perTag) in _data)
        {
            foreach (var (anchorId, entry) in perTag)
            {
                if (entry.RecordedAt < cutoff)
                    perTag.TryRemove(anchorId, out _);
            }
            if (perTag.IsEmpty)
                _data.TryRemove(tagKey, out _);
        }
    }

    public void ClearSession(Guid sessionId)
    {
        foreach (var key in _data.Keys)
        {
            if (key.SessionId == sessionId)
                _data.TryRemove(key, out _);
        }
    }
}
