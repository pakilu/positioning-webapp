using App.BLL.Positioning;
using App.DAL.EF;
using App.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace App.BLL.Tests.Positioning;

public class PositioningPipelineTests
{
    // --- Test doubles ------------------------------------------------------

    private sealed class CapturingPublisher : IPositionResultPublisher
    {
        public List<PositionResultBroadcast> Broadcasts { get; } = new();
        public Task PublishAsync(PositionResultBroadcast b, CancellationToken ct = default)
        {
            Broadcasts.Add(b);
            return Task.CompletedTask;
        }
    }

    /// <summary>Manual clock so we can step time deterministically.</summary>
    private sealed class TestClock : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);
        public override DateTimeOffset GetUtcNow() => Now;
        public void Advance(TimeSpan dt) => Now = Now.Add(dt);
    }

    // --- Fixture builder ---------------------------------------------------

    private sealed class Fixture
    {
        public required PositioningPipeline Pipeline { get; init; }
        public required CapturingPublisher Publisher { get; init; }
        public required TestClock Clock { get; init; }
        public required IServiceProvider Sp { get; init; }
        public required Guid SessionId { get; init; }
        public required Guid TagId { get; init; }
        public required Guid A1 { get; init; }
        public required Guid A2 { get; init; }
        public required Guid A3 { get; init; }
        public required Guid A4 { get; init; }
    }

    private static async Task<Fixture> BuildAsync(
        string dbName,
        PositioningPipelineOptions? opts = null)
    {
        var services = new ServiceCollection();
        services.AddDbContext<AppDbContext>(o => o.UseInMemoryDatabase(dbName));
        var sp = services.BuildServiceProvider();
        var scopes = sp.GetRequiredService<IServiceScopeFactory>();

        // Seed: 4 anchors at the ceiling, 1 tag, in one session.
        Guid sessionId, tagId, a1, a2, a3, a4;
        using (var scope = scopes.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var c1 = new Chip { Name = "A1", DeviceIdentifier = "a1" };
            var c2 = new Chip { Name = "A2", DeviceIdentifier = "a2" };
            var c3 = new Chip { Name = "A3", DeviceIdentifier = "a3" };
            var c4 = new Chip { Name = "A4", DeviceIdentifier = "a4" };
            var ct = new Chip { Name = "T",  DeviceIdentifier = "t"  };

            var cfg = new SessionConfig { Name = "cfg" };
            cfg.SessionConfigChips.Add(new SessionConfigChip { Chip = c1, Role = EChipRole.Anchor, XCoord = 0,  YCoord = 0,  ZCoord = 2.5m });
            cfg.SessionConfigChips.Add(new SessionConfigChip { Chip = c2, Role = EChipRole.Anchor, XCoord = 10, YCoord = 0,  ZCoord = 2.5m });
            cfg.SessionConfigChips.Add(new SessionConfigChip { Chip = c3, Role = EChipRole.Anchor, XCoord = 10, YCoord = 10, ZCoord = 2.5m });
            cfg.SessionConfigChips.Add(new SessionConfigChip { Chip = c4, Role = EChipRole.Anchor, XCoord = 0,  YCoord = 10, ZCoord = 2.5m });
            cfg.SessionConfigChips.Add(new SessionConfigChip { Chip = ct, Role = EChipRole.Tag });
            var s = new Session { Name = "s", SessionConfig = cfg };
            db.AddRange(c1, c2, c3, c4, ct, cfg, s);
            await db.SaveChangesAsync();
            sessionId = s.Id; tagId = ct.Id;
            a1 = c1.Id; a2 = c2.Id; a3 = c3.Id; a4 = c4.Id;
        }

        opts ??= new PositioningPipelineOptions
        {
            MinSolveInterval = TimeSpan.Zero, // disable throttle by default
            MaxMeasurementAge = TimeSpan.FromSeconds(2),
            Solver = new TrilaterationOptions { Mode = SolverMode.TwoD, TagZ = 0 },
        };

        var publisher = new CapturingPublisher();
        var clock = new TestClock();

        var pipeline = new PositioningPipeline(
            buffer: new InMemoryMeasurementBuffer(),
            anchorProvider: new AnchorPositionProvider(scopes),
            solver: new LeastSquaresTrilaterationSolver(),
            publisher: publisher,
            scopeFactory: scopes,
            opts: opts,
            clock: clock,
            log: NullLogger<PositioningPipeline>.Instance);

        return new Fixture
        {
            Pipeline = pipeline, Publisher = publisher, Clock = clock, Sp = sp,
            SessionId = sessionId, TagId = tagId, A1 = a1, A2 = a2, A3 = a3, A4 = a4,
        };
    }

    // Synthetic 2D distance from anchor at (x,y,2.5) to tag at (tagX,tagY,0).
    private static double Dist(double ax, double ay, double tagX, double tagY)
        => Math.Sqrt((ax - tagX) * (ax - tagX) + (ay - tagY) * (ay - tagY) + 2.5 * 2.5);

    // --- Tests -------------------------------------------------------------

    [Fact]
    public async Task TooFewAnchors_NoSolve()
    {
        var f = await BuildAsync(nameof(TooFewAnchors_NoSolve));
        var t = f.Clock.Now.UtcDateTime;

        var r1 = await f.Pipeline.OnRawMeasurementAsync(f.SessionId, f.TagId, f.A1, Dist(0, 0, 3, 4), t);
        var r2 = await f.Pipeline.OnRawMeasurementAsync(f.SessionId, f.TagId, f.A2, Dist(10, 0, 3, 4), t);

        Assert.Null(r1);
        Assert.Null(r2);
        Assert.Empty(f.Publisher.Broadcasts);
    }

    [Fact]
    public async Task ThreeAnchors_ProducesFixAndBroadcasts()
    {
        var f = await BuildAsync(nameof(ThreeAnchors_ProducesFixAndBroadcasts));
        var t = f.Clock.Now.UtcDateTime;
        const double tagX = 3, tagY = 4;

        await f.Pipeline.OnRawMeasurementAsync(f.SessionId, f.TagId, f.A1, Dist(0,  0,  tagX, tagY), t);
        await f.Pipeline.OnRawMeasurementAsync(f.SessionId, f.TagId, f.A2, Dist(10, 0,  tagX, tagY), t);
        var result = await f.Pipeline.OnRawMeasurementAsync(f.SessionId, f.TagId, f.A3, Dist(10, 10, tagX, tagY), t);

        Assert.NotNull(result);
        Assert.Equal(tagX, result!.X, 4);
        Assert.Equal(tagY, result.Y, 4);
        Assert.Equal(3, result.AnchorCount);

        Assert.Single(f.Publisher.Broadcasts);
        Assert.Equal(result, f.Publisher.Broadcasts[0]);
    }

    [Fact]
    public async Task SolvedFix_IsPersistedToDb()
    {
        var f = await BuildAsync(nameof(SolvedFix_IsPersistedToDb));
        var t = f.Clock.Now.UtcDateTime;
        const double tagX = 5.5, tagY = 1.8;

        await f.Pipeline.OnRawMeasurementAsync(f.SessionId, f.TagId, f.A1, Dist(0,  0,  tagX, tagY), t);
        await f.Pipeline.OnRawMeasurementAsync(f.SessionId, f.TagId, f.A2, Dist(10, 0,  tagX, tagY), t);
        await f.Pipeline.OnRawMeasurementAsync(f.SessionId, f.TagId, f.A3, Dist(10, 10, tagX, tagY), t);
        await f.Pipeline.OnRawMeasurementAsync(f.SessionId, f.TagId, f.A4, Dist(0,  10, tagX, tagY), t);

        using var scope = f.Sp.GetRequiredService<IServiceScopeFactory>().CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var rows = await db.PositionResults.Where(p => p.SessionId == f.SessionId).ToListAsync();

        Assert.NotEmpty(rows);
        var last = rows[^1];
        Assert.Equal(f.TagId, last.TagChipId);
        Assert.Equal(tagX, (double)last.XCoord, 3);
        Assert.Equal(tagY, (double)last.YCoord, 3);
    }

    [Fact]
    public async Task PersistResultsFalse_BroadcastsButDoesNotPersist()
    {
        var f = await BuildAsync(
            nameof(PersistResultsFalse_BroadcastsButDoesNotPersist),
            new PositioningPipelineOptions
            {
                PersistResults = false,
                MinSolveInterval = TimeSpan.Zero,
                MaxMeasurementAge = TimeSpan.FromSeconds(2),
                Solver = new TrilaterationOptions { Mode = SolverMode.TwoD, TagZ = 0 },
            });
        var t = f.Clock.Now.UtcDateTime;

        await f.Pipeline.OnRawMeasurementAsync(f.SessionId, f.TagId, f.A1, Dist(0,  0,  3, 4), t);
        await f.Pipeline.OnRawMeasurementAsync(f.SessionId, f.TagId, f.A2, Dist(10, 0,  3, 4), t);
        await f.Pipeline.OnRawMeasurementAsync(f.SessionId, f.TagId, f.A3, Dist(10, 10, 3, 4), t);

        Assert.Single(f.Publisher.Broadcasts);

        using var scope = f.Sp.GetRequiredService<IServiceScopeFactory>().CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Empty(await db.PositionResults.ToListAsync());
    }

    [Fact]
    public async Task Throttle_LimitsBroadcastRate()
    {
        var f = await BuildAsync(
            nameof(Throttle_LimitsBroadcastRate),
            new PositioningPipelineOptions
            {
                MinSolveInterval = TimeSpan.FromMilliseconds(500),
                MaxMeasurementAge = TimeSpan.FromSeconds(2),
                PersistResults = false,
                Solver = new TrilaterationOptions { Mode = SolverMode.TwoD, TagZ = 0 },
            });
        var t = f.Clock.Now.UtcDateTime;
        const double tagX = 3, tagY = 4;

        // Round 1: should produce one broadcast.
        await f.Pipeline.OnRawMeasurementAsync(f.SessionId, f.TagId, f.A1, Dist(0,  0,  tagX, tagY), t);
        await f.Pipeline.OnRawMeasurementAsync(f.SessionId, f.TagId, f.A2, Dist(10, 0,  tagX, tagY), t);
        await f.Pipeline.OnRawMeasurementAsync(f.SessionId, f.TagId, f.A3, Dist(10, 10, tagX, tagY), t);
        await f.Pipeline.OnRawMeasurementAsync(f.SessionId, f.TagId, f.A4, Dist(0,  10, tagX, tagY), t);
        Assert.Single(f.Publisher.Broadcasts);

        // Round 2 immediately after: should be throttled (no extra broadcasts).
        await f.Pipeline.OnRawMeasurementAsync(f.SessionId, f.TagId, f.A1, Dist(0,  0,  tagX, tagY), t);
        await f.Pipeline.OnRawMeasurementAsync(f.SessionId, f.TagId, f.A2, Dist(10, 0,  tagX, tagY), t);
        Assert.Single(f.Publisher.Broadcasts);

        // Advance past the throttle window: next round produces another broadcast.
        f.Clock.Advance(TimeSpan.FromMilliseconds(600));
        var t2 = f.Clock.Now.UtcDateTime;
        await f.Pipeline.OnRawMeasurementAsync(f.SessionId, f.TagId, f.A1, Dist(0,  0,  tagX, tagY), t2);
        await f.Pipeline.OnRawMeasurementAsync(f.SessionId, f.TagId, f.A2, Dist(10, 0,  tagX, tagY), t2);
        await f.Pipeline.OnRawMeasurementAsync(f.SessionId, f.TagId, f.A3, Dist(10, 10, tagX, tagY), t2);
        Assert.Equal(2, f.Publisher.Broadcasts.Count);
    }

    [Fact]
    public async Task UnknownAnchor_IsIgnoredInJoin()
    {
        var f = await BuildAsync(nameof(UnknownAnchor_IsIgnoredInJoin));
        var t = f.Clock.Now.UtcDateTime;
        const double tagX = 3, tagY = 4;

        // A stray anchor not in the config — should be silently dropped during the join.
        var strayAnchor = Guid.NewGuid();
        await f.Pipeline.OnRawMeasurementAsync(f.SessionId, f.TagId, strayAnchor, 99.0, t);

        // Only 2 valid anchors yet → not enough.
        await f.Pipeline.OnRawMeasurementAsync(f.SessionId, f.TagId, f.A1, Dist(0,  0,  tagX, tagY), t);
        await f.Pipeline.OnRawMeasurementAsync(f.SessionId, f.TagId, f.A2, Dist(10, 0,  tagX, tagY), t);
        Assert.Empty(f.Publisher.Broadcasts);

        // Add a 3rd valid anchor → should solve, ignoring the stray.
        var fix = await f.Pipeline.OnRawMeasurementAsync(f.SessionId, f.TagId, f.A3, Dist(10, 10, tagX, tagY), t);
        Assert.NotNull(fix);
        Assert.Equal(3, fix!.AnchorCount);
        Assert.Equal(tagX, fix.X, 4);
    }

    [Fact]
    public async Task StaleMeasurements_AreDroppedFromSnapshot()
    {
        var f = await BuildAsync(
            nameof(StaleMeasurements_AreDroppedFromSnapshot),
            new PositioningPipelineOptions
            {
                MinSolveInterval = TimeSpan.Zero,
                MaxMeasurementAge = TimeSpan.FromMilliseconds(500),
                PersistResults = false,
                Solver = new TrilaterationOptions { Mode = SolverMode.TwoD, TagZ = 0 },
            });

        // Old measurements at t0 ...
        var t0 = f.Clock.Now.UtcDateTime;
        await f.Pipeline.OnRawMeasurementAsync(f.SessionId, f.TagId, f.A1, Dist(0,  0,  3, 4), t0);
        await f.Pipeline.OnRawMeasurementAsync(f.SessionId, f.TagId, f.A2, Dist(10, 0,  3, 4), t0);

        // ... clock jumps past the TTL ...
        f.Clock.Advance(TimeSpan.FromSeconds(2));
        var t1 = f.Clock.Now.UtcDateTime;

        // Only one fresh anchor: the two stale ones must be excluded, so no solve.
        var result = await f.Pipeline.OnRawMeasurementAsync(f.SessionId, f.TagId, f.A3, Dist(10, 10, 3, 4), t1);
        Assert.Null(result);
        Assert.Empty(f.Publisher.Broadcasts);
    }
}
