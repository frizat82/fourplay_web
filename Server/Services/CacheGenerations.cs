using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Primitives;

namespace FourPlayWebApp.Server.Services;

/// <summary>
/// Makes IMemoryCache invalidation safe against reads already in flight. One cancellation
/// "generation" per scope (e.g. a league-season, or the schedule tables), per cache instance.
/// Every derived entry carries the token that was current when its read STARTED; invalidation
/// cancels it. So a read that began before a write stores an already-expired entry instead of
/// re-caching the old value for its full TTL after the writer's Remove has run. Keyed by the
/// cache (not one process-wide map) so separate caches — e.g. parallel test classes — can't
/// expire each other's entries, and the map is collected with its cache.
/// </summary>
public static class CacheGenerations {
    private static readonly ConditionalWeakTable<IMemoryCache, ConcurrentDictionary<object, CancellationTokenSource>> Generations = new();

    private static ConcurrentDictionary<object, CancellationTokenSource> For(IMemoryCache cache) =>
        Generations.GetValue(cache, _ => new());

    /// <summary>Tie a cache entry to <paramref name="scope"/>; call before reading the source data.</summary>
    public static void Track(IMemoryCache cache, ICacheEntry entry, object scope) =>
        entry.AddExpirationToken(new CancellationChangeToken(
            For(cache).GetOrAdd(scope, _ => new CancellationTokenSource()).Token));

    /// <summary>Expire every entry tracked under <paramref name="scope"/>, including in-flight reads.</summary>
    public static void Invalidate(IMemoryCache cache, object scope) {
        if (For(cache).TryRemove(scope, out var generation)) generation.Cancel();
    }
}
