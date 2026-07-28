using App.BLL.Positioning;
using App.DAL.EF;
using App.Domain;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace App.BLL.Tests.Positioning;

public class AnchorRoutingServiceTests
{
    private sealed record Fixture(
        AppDbContext Db,
        AnchorRoutingService Service,
        Guid SessionId,
        Guid TagId);

    private static async Task<Fixture> BuildAsync(string dbName)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options;
        var db = new AppDbContext(options);

        var tag = new Chip { Name = "Tag", DeviceIdentifier = "TAG-01" };
        var west = new Chip { Name = "West", DeviceIdentifier = "ANC-W" };
        var east = new Chip { Name = "East", DeviceIdentifier = "ANC-E" };
        var north = new Chip { Name = "North", DeviceIdentifier = "ANC-N" };
        var south = new Chip { Name = "South", DeviceIdentifier = "ANC-S" };
        var farEast = new Chip { Name = "Far East", DeviceIdentifier = "ANC-FE" };

        var cfg = new SessionConfig { Name = "cfg" };
        cfg.SessionConfigChips.Add(new SessionConfigChip { Chip = west, Role = EChipRole.Anchor, XCoord = 0, YCoord = 5, ZCoord = 2.5m });
        cfg.SessionConfigChips.Add(new SessionConfigChip { Chip = east, Role = EChipRole.Anchor, XCoord = 10, YCoord = 5, ZCoord = 2.5m });
        cfg.SessionConfigChips.Add(new SessionConfigChip { Chip = north, Role = EChipRole.Anchor, XCoord = 5, YCoord = 10, ZCoord = 2.5m });
        cfg.SessionConfigChips.Add(new SessionConfigChip { Chip = south, Role = EChipRole.Anchor, XCoord = 5, YCoord = 0, ZCoord = 2.5m });
        cfg.SessionConfigChips.Add(new SessionConfigChip { Chip = farEast, Role = EChipRole.Anchor, XCoord = 15, YCoord = 5, ZCoord = 2.5m });
        cfg.SessionConfigChips.Add(new SessionConfigChip { Chip = tag, Role = EChipRole.Tag });

        var session = new Session { Name = "session", SessionConfig = cfg };
        db.AddRange(tag, west, east, north, south, farEast, cfg, session);
        await db.SaveChangesAsync();

        return new Fixture(db, new AnchorRoutingService(db), session.Id, tag.Id);
    }

    [Fact]
    public async Task NoPosition_ReturnsBootstrapAnchors()
    {
        var f = await BuildAsync(nameof(NoPosition_ReturnsBootstrapAnchors));

        var decision = await f.Service.GetNextAnchorsAsync(f.SessionId, "TAG-01", desiredCount: 3);

        Assert.NotNull(decision);
        Assert.False(decision!.PositionKnown);
        Assert.False(decision.MovementKnown);
        Assert.Equal(3, decision.Anchors.Count);
    }

    [Fact]
    public async Task LatestPosition_SelectsNearbyAnchorsWithSpread()
    {
        var f = await BuildAsync(nameof(LatestPosition_SelectsNearbyAnchorsWithSpread));
        f.Db.PositionResults.Add(new PositionResult
        {
            SessionId = f.SessionId,
            TagChipId = f.TagId,
            RecordedAt = new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc),
            XCoord = 4.8m,
            YCoord = 5.1m,
            ZCoord = 0m,
        });
        await f.Db.SaveChangesAsync();

        var decision = await f.Service.GetNextAnchorsAsync(f.SessionId, "TAG-01", desiredCount: 3);

        Assert.NotNull(decision);
        Assert.True(decision!.PositionKnown);
        Assert.False(decision.MovementKnown);
        Assert.Equal(3, decision.Anchors.Count);
        Assert.Contains(decision.Anchors, a => a.DeviceIdentifier is "ANC-W" or "ANC-E");
    }

    [Fact]
    public async Task MovingEast_PrefersAnchorAheadOfTag()
    {
        var f = await BuildAsync(nameof(MovingEast_PrefersAnchorAheadOfTag));
        f.Db.PositionResults.AddRange(
            new PositionResult
            {
                SessionId = f.SessionId,
                TagChipId = f.TagId,
                RecordedAt = new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc),
                XCoord = 4m,
                YCoord = 5m,
                ZCoord = 0m,
            },
            new PositionResult
            {
                SessionId = f.SessionId,
                TagChipId = f.TagId,
                RecordedAt = new DateTime(2026, 1, 1, 12, 0, 1, DateTimeKind.Utc),
                XCoord = 6m,
                YCoord = 5m,
                ZCoord = 0m,
            });
        await f.Db.SaveChangesAsync();

        var decision = await f.Service.GetNextAnchorsAsync(f.SessionId, "TAG-01", desiredCount: 3);

        Assert.NotNull(decision);
        Assert.True(decision!.MovementKnown);
        Assert.Contains(decision.Anchors, a => a.DeviceIdentifier == "ANC-FE");
    }

    [Fact]
    public async Task UnknownTag_ReturnsNull()
    {
        var f = await BuildAsync(nameof(UnknownTag_ReturnsNull));

        var decision = await f.Service.GetNextAnchorsAsync(f.SessionId, "NOPE", desiredCount: 3);

        Assert.Null(decision);
    }
}
