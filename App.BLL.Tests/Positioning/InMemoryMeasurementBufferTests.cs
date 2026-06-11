using App.BLL.Positioning;
using Xunit;

namespace App.BLL.Tests.Positioning;

public class InMemoryMeasurementBufferTests
{
    private static readonly DateTime T0 = new(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Snapshot_EmptyBuffer_ReturnsEmpty()
    {
        var buf = new InMemoryMeasurementBuffer();
        Assert.Empty(buf.Snapshot(Guid.NewGuid(), Guid.NewGuid(), TimeSpan.FromSeconds(1), T0));
    }

    [Fact]
    public void Add_LatestPerAnchor_OverwritesPrevious()
    {
        var buf = new InMemoryMeasurementBuffer();
        var session = Guid.NewGuid();
        var tag = Guid.NewGuid();
        var a = Guid.NewGuid();

        buf.Add(session, tag, a, 1.0, T0);
        buf.Add(session, tag, a, 2.0, T0.AddMilliseconds(100));

        var snap = buf.Snapshot(session, tag, TimeSpan.FromSeconds(1), T0.AddMilliseconds(150));
        Assert.Single(snap);
        Assert.Equal(2.0, snap[0].Distance);
    }

    [Fact]
    public void Add_OutOfOrderMessage_DoesNotOverwriteNewer()
    {
        var buf = new InMemoryMeasurementBuffer();
        var s = Guid.NewGuid(); var t = Guid.NewGuid(); var a = Guid.NewGuid();

        buf.Add(s, t, a, 5.0, T0.AddMilliseconds(200));
        buf.Add(s, t, a, 1.0, T0.AddMilliseconds(100)); // late arrival, older timestamp

        var snap = buf.Snapshot(s, t, TimeSpan.FromSeconds(1), T0.AddMilliseconds(300));
        Assert.Single(snap);
        Assert.Equal(5.0, snap[0].Distance);
    }

    [Fact]
    public void Snapshot_OneEntryPerAnchor_AcrossMultipleAnchors()
    {
        var buf = new InMemoryMeasurementBuffer();
        var s = Guid.NewGuid(); var t = Guid.NewGuid();
        var a1 = Guid.NewGuid(); var a2 = Guid.NewGuid(); var a3 = Guid.NewGuid();

        buf.Add(s, t, a1, 1.0, T0);
        buf.Add(s, t, a2, 2.0, T0);
        buf.Add(s, t, a3, 3.0, T0);

        var snap = buf.Snapshot(s, t, TimeSpan.FromSeconds(1), T0.AddMilliseconds(50));
        Assert.Equal(3, snap.Count);
        Assert.Equal(new HashSet<Guid> { a1, a2, a3 }, snap.Select(x => x.AnchorId).ToHashSet());
    }

    [Fact]
    public void Snapshot_FiltersOutStaleEntries()
    {
        var buf = new InMemoryMeasurementBuffer();
        var s = Guid.NewGuid(); var t = Guid.NewGuid();
        var fresh = Guid.NewGuid(); var stale = Guid.NewGuid();

        buf.Add(s, t, stale, 9.0, T0);                          // very old
        buf.Add(s, t, fresh, 1.0, T0.AddSeconds(10));           // recent

        var snap = buf.Snapshot(s, t, TimeSpan.FromMilliseconds(500), T0.AddSeconds(10.1));
        Assert.Single(snap);
        Assert.Equal(fresh, snap[0].AnchorId);
    }

    [Fact]
    public void Snapshot_IsolatesPerTagAndPerSession()
    {
        var buf = new InMemoryMeasurementBuffer();
        var s1 = Guid.NewGuid(); var s2 = Guid.NewGuid();
        var t1 = Guid.NewGuid(); var t2 = Guid.NewGuid();
        var a = Guid.NewGuid();

        buf.Add(s1, t1, a, 1.0, T0);
        buf.Add(s1, t2, a, 2.0, T0);
        buf.Add(s2, t1, a, 3.0, T0);

        Assert.Equal(1.0, buf.Snapshot(s1, t1, TimeSpan.FromSeconds(1), T0)[0].Distance);
        Assert.Equal(2.0, buf.Snapshot(s1, t2, TimeSpan.FromSeconds(1), T0)[0].Distance);
        Assert.Equal(3.0, buf.Snapshot(s2, t1, TimeSpan.FromSeconds(1), T0)[0].Distance);
        Assert.Empty(buf.Snapshot(s2, t2, TimeSpan.FromSeconds(1), T0));
    }

    [Fact]
    public void Prune_RemovesOldEntriesAndEmptyTagBuckets()
    {
        var buf = new InMemoryMeasurementBuffer();
        var s = Guid.NewGuid(); var t = Guid.NewGuid();
        var old = Guid.NewGuid(); var fresh = Guid.NewGuid();

        buf.Add(s, t, old,   1.0, T0);
        buf.Add(s, t, fresh, 2.0, T0.AddSeconds(5));

        buf.Prune(TimeSpan.FromSeconds(1), T0.AddSeconds(5.5));

        var snap = buf.Snapshot(s, t, TimeSpan.FromSeconds(10), T0.AddSeconds(5.5));
        Assert.Single(snap);
        Assert.Equal(fresh, snap[0].AnchorId);

        // Pruning a now-untouched tag fully should empty the outer dict bucket too.
        buf.Prune(TimeSpan.Zero, T0.AddSeconds(100));
        Assert.Empty(buf.Snapshot(s, t, TimeSpan.FromSeconds(10), T0.AddSeconds(100)));
    }

    [Fact]
    public void ClearSession_RemovesOnlyThatSession()
    {
        var buf = new InMemoryMeasurementBuffer();
        var s1 = Guid.NewGuid(); var s2 = Guid.NewGuid();
        var t = Guid.NewGuid(); var a = Guid.NewGuid();

        buf.Add(s1, t, a, 1.0, T0);
        buf.Add(s2, t, a, 2.0, T0);

        buf.ClearSession(s1);

        Assert.Empty(buf.Snapshot(s1, t, TimeSpan.FromSeconds(1), T0));
        Assert.Single(buf.Snapshot(s2, t, TimeSpan.FromSeconds(1), T0));
    }

    [Fact]
    public async Task Add_IsThreadSafe_UnderConcurrentWriters()
    {
        var buf = new InMemoryMeasurementBuffer();
        var s = Guid.NewGuid(); var t = Guid.NewGuid();
        var anchors = Enumerable.Range(0, 10).Select(_ => Guid.NewGuid()).ToArray();

        // 10 anchors × 1000 updates each from parallel tasks.
        await Task.WhenAll(anchors.Select((a, idx) => Task.Run(() =>
        {
            for (int i = 0; i < 1000; i++)
                buf.Add(s, t, a, i, T0.AddMilliseconds(i));
        })));

        var snap = buf.Snapshot(s, t, TimeSpan.FromHours(1), T0.AddSeconds(10));
        Assert.Equal(anchors.Length, snap.Count);
        // Each anchor should end up with the last (max-timestamp) value, which is 999.
        Assert.All(snap, x => Assert.Equal(999.0, x.Distance));
    }
}
