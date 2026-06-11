using System.Collections.Concurrent;
using App.DAL.EF;
using App.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace App.BLL.Positioning;

/// <summary>
/// Database-backed <see cref="IAnchorPositionProvider"/> with per-session caching.
///
/// Lifetime: singleton. Uses <see cref="IServiceScopeFactory"/> to open a scoped
/// <see cref="AppDbContext"/> on the rare occasions it actually has to hit the DB
/// (cache miss or after <see cref="Invalidate"/>).
///
/// Concurrency: many tag measurements may arrive in parallel; the
/// <c>Lazy&lt;Task&lt;...&gt;&gt;</c> pattern guarantees the first concurrent
/// request triggers exactly one DB query while later callers await the same task.
/// </summary>
public sealed class AnchorPositionProvider : IAnchorPositionProvider
{
    private readonly IServiceScopeFactory _scopeFactory;

    private readonly ConcurrentDictionary<Guid, Lazy<Task<IReadOnlyDictionary<Guid, AnchorPosition>>>> _cache
        = new();

    public AnchorPositionProvider(IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory;
    }

    public Task<IReadOnlyDictionary<Guid, AnchorPosition>> GetAnchorsAsync(
        Guid sessionId, CancellationToken ct = default)
    {
        var lazy = _cache.GetOrAdd(sessionId, id =>
            new Lazy<Task<IReadOnlyDictionary<Guid, AnchorPosition>>>(
                () => LoadAsync(id, ct)));

        return lazy.Value;
    }

    public void Invalidate(Guid sessionId) => _cache.TryRemove(sessionId, out _);

    private async Task<IReadOnlyDictionary<Guid, AnchorPosition>> LoadAsync(
        Guid sessionId, CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        // Session → SessionConfig → SessionConfigChips (anchors with coordinates).
        // Tags are filtered out; anchors with missing X/Y are skipped defensively
        // (the SessionConfigService validator already enforces this on save).
        var anchors = await db.Sessions
            .Where(s => s.Id == sessionId)
            .SelectMany(s => s.SessionConfig.SessionConfigChips)
            .Where(scc => scc.Role == EChipRole.Anchor
                          && scc.XCoord != null
                          && scc.YCoord != null)
            .Select(scc => new
            {
                scc.ChipId,
                X = (double)scc.XCoord!.Value,
                Y = (double)scc.YCoord!.Value,
                Z = scc.ZCoord != null ? (double)scc.ZCoord.Value : 0.0,
            })
            .ToListAsync(ct);

        var dict = new Dictionary<Guid, AnchorPosition>(anchors.Count);
        foreach (var a in anchors)
            dict[a.ChipId] = new AnchorPosition(a.X, a.Y, a.Z);

        return dict;
    }
}
