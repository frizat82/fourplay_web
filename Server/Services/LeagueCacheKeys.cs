using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Primitives;

namespace FourPlayWebApp.Server.Services;

/// <summary>
/// IMemoryCache keys for data derived from a league's per-season settings (LeagueJuiceMapping), in
/// one place so the writers that change those settings can evict exactly what the readers cached.
/// </summary>
public static class LeagueCacheKeys {
    /// <summary>A league's juice mapping for one season (SpreadCalculatorProvider).</summary>
    public static string Juice(int leagueId, int season) => $"juice_{leagueId}_{season}";

    /// <summary>A league's built leaderboard for one season (LeaderboardController).</summary>
    public static string Leaderboard(int leagueId, long season) => $"{leagueId}-{season}";

    // One cancellation "generation" per (league, season), per cache instance. Every derived entry
    // carries the token that was current when its read STARTED; invalidation cancels it. So a read
    // already in flight when the settings change stores an already-expired entry instead of
    // re-caching the old value for its full TTL after the Remove below has run. Keyed by the cache
    // (not one process-wide map) so separate caches — e.g. parallel test classes — can't expire
    // each other's entries, and the map is collected with its cache.
    private static readonly ConditionalWeakTable<IMemoryCache, ConcurrentDictionary<(int LeagueId, long Season), CancellationTokenSource>> Generations = new();

    private static ConcurrentDictionary<(int LeagueId, long Season), CancellationTokenSource> For(IMemoryCache cache) =>
        Generations.GetValue(cache, _ => new());

    /// <summary>Tie a cache entry to the league-season's settings; call before reading them.</summary>
    public static void Track(IMemoryCache cache, ICacheEntry entry, int leagueId, long season) =>
        entry.AddExpirationToken(new CancellationChangeToken(
            For(cache).GetOrAdd((leagueId, season), _ => new CancellationTokenSource()).Token));

    /// <summary>
    /// Call after a league's settings for <paramref name="season"/> change. LeagueRepository's
    /// juice writers do, so every writer (endpoints, jobs, league creation) is covered.
    /// </summary>
    public static void InvalidateLeagueSeason(IMemoryCache cache, int leagueId, int season) {
        if (For(cache).TryRemove((leagueId, season), out var generation)) generation.Cancel();
        cache.Remove(Juice(leagueId, season));
        cache.Remove(Leaderboard(leagueId, season));
    }
}
