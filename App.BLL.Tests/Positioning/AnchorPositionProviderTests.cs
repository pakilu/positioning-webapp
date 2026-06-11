using App.BLL.Positioning;
using App.DAL.EF;
using App.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace App.BLL.Tests.Positioning;

public class AnchorPositionProviderTests
{
    // Each test gets its own service provider / in-memory DB so they're isolated.
    private static (IServiceProvider Sp, IServiceScopeFactory ScopeFactory) BuildServices(string dbName)
    {
        var services = new ServiceCollection();
        services.AddDbContext<AppDbContext>(o => o.UseInMemoryDatabase(dbName));
        var sp = services.BuildServiceProvider();
        return (sp, sp.GetRequiredService<IServiceScopeFactory>());
    }

    private static async Task<(Guid sessionId, Guid a1, Guid a2, Guid a3, Guid tag)>
        SeedAsync(IServiceScopeFactory scopes, bool includeTag = true)
    {
        using var scope = scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var chipA1 = new Chip { Name = "A1", DeviceIdentifier = "dev-a1" };
        var chipA2 = new Chip { Name = "A2", DeviceIdentifier = "dev-a2" };
        var chipA3 = new Chip { Name = "A3", DeviceIdentifier = "dev-a3" };
        var chipTag = new Chip { Name = "Tag", DeviceIdentifier = "dev-tag" };

        var config = new SessionConfig { Name = "cfg" };
        config.SessionConfigChips.Add(new SessionConfigChip
            { Chip = chipA1, Role = EChipRole.Anchor, XCoord = 0,  YCoord = 0,  ZCoord = 2.5m });
        config.SessionConfigChips.Add(new SessionConfigChip
            { Chip = chipA2, Role = EChipRole.Anchor, XCoord = 10, YCoord = 0,  ZCoord = 2.5m });
        config.SessionConfigChips.Add(new SessionConfigChip
            { Chip = chipA3, Role = EChipRole.Anchor, XCoord = 0,  YCoord = 10, ZCoord = 2.5m });
        if (includeTag)
        {
            config.SessionConfigChips.Add(new SessionConfigChip
                { Chip = chipTag, Role = EChipRole.Tag });
        }

        var session = new Session { Name = "s", SessionConfig = config };

        db.AddRange(chipA1, chipA2, chipA3, chipTag, config, session);
        await db.SaveChangesAsync();

        return (session.Id, chipA1.Id, chipA2.Id, chipA3.Id, chipTag.Id);
    }

    [Fact]
    public async Task GetAnchorsAsync_ReturnsOnlyAnchors_WithCorrectCoordinates()
    {
        var (sp, scopes) = BuildServices(nameof(GetAnchorsAsync_ReturnsOnlyAnchors_WithCorrectCoordinates));
        var (sessionId, a1, a2, a3, tag) = await SeedAsync(scopes);

        var provider = new AnchorPositionProvider(scopes);
        var anchors = await provider.GetAnchorsAsync(sessionId);

        Assert.Equal(3, anchors.Count);
        Assert.False(anchors.ContainsKey(tag));

        Assert.Equal(new AnchorPosition(0,  0,  2.5), anchors[a1]);
        Assert.Equal(new AnchorPosition(10, 0,  2.5), anchors[a2]);
        Assert.Equal(new AnchorPosition(0,  10, 2.5), anchors[a3]);
    }

    [Fact]
    public async Task GetAnchorsAsync_UnknownSession_ReturnsEmpty()
    {
        var (_, scopes) = BuildServices(nameof(GetAnchorsAsync_UnknownSession_ReturnsEmpty));
        var provider = new AnchorPositionProvider(scopes);

        var anchors = await provider.GetAnchorsAsync(Guid.NewGuid());

        Assert.Empty(anchors);
    }

    [Fact]
    public async Task GetAnchorsAsync_Cached_DoesNotHitDbTwice()
    {
        // After the first call, mutating the DB without invalidating should be invisible.
        var (sp, scopes) = BuildServices(nameof(GetAnchorsAsync_Cached_DoesNotHitDbTwice));
        var (sessionId, a1, _, _, _) = await SeedAsync(scopes);

        var provider = new AnchorPositionProvider(scopes);
        var first = await provider.GetAnchorsAsync(sessionId);
        Assert.Equal(0.0, first[a1].X);

        // Mutate underlying data.
        using (var scope = scopes.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var row = db.SessionConfigChips.Single(x => x.ChipId == a1);
            row.XCoord = 999;
            await db.SaveChangesAsync();
        }

        var second = await provider.GetAnchorsAsync(sessionId);
        Assert.Equal(0.0, second[a1].X); // cached, NOT 999
    }

    [Fact]
    public async Task Invalidate_ForcesReloadOnNextCall()
    {
        var (sp, scopes) = BuildServices(nameof(Invalidate_ForcesReloadOnNextCall));
        var (sessionId, a1, _, _, _) = await SeedAsync(scopes);

        var provider = new AnchorPositionProvider(scopes);
        _ = await provider.GetAnchorsAsync(sessionId); // prime the cache

        using (var scope = scopes.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var row = db.SessionConfigChips.Single(x => x.ChipId == a1);
            row.XCoord = 42;
            await db.SaveChangesAsync();
        }

        provider.Invalidate(sessionId);

        var after = await provider.GetAnchorsAsync(sessionId);
        Assert.Equal(42.0, after[a1].X);
    }

    [Fact]
    public async Task ConcurrentFirstCalls_TriggerOnlyOneLoad()
    {
        // The Lazy<Task<...>> guarantees a single DB hit even when many requests
        // for the same session race on a cold cache.
        var (sp, scopes) = BuildServices(nameof(ConcurrentFirstCalls_TriggerOnlyOneLoad));
        var (sessionId, _, _, _, _) = await SeedAsync(scopes);

        var provider = new AnchorPositionProvider(scopes);

        var tasks = Enumerable.Range(0, 32)
            .Select(_ => Task.Run(() => provider.GetAnchorsAsync(sessionId)))
            .ToArray();

        var results = await Task.WhenAll(tasks);

        // All results must be the very same cached instance.
        var first = results[0];
        Assert.All(results, r => Assert.Same(first, r));
    }
}
