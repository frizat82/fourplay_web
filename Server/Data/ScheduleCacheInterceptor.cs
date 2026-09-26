using System.Runtime.CompilerServices;
using FourPlayWebApp.Server.Models.Data;
using FourPlayWebApp.Server.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Caching.Memory;

namespace FourPlayWebApp.Server.Data;

/// <summary>
/// Evicts <see cref="ScheduleCache"/> for any schedule table a save touches — whoever saves:
/// repositories, DemoDataSeeder's direct writes, any future writer — so correctness doesn't depend
/// on each writer remembering to evict. Eviction happens after the save commits (evicting before
/// would let a read in between re-cache the old rows). Bulk ExecuteUpdate/ExecuteDelete bypass
/// interceptors; none target these tables. Out-of-band SQL is covered by ScheduleCache's TTL.
/// </summary>
public sealed class ScheduleCacheInterceptor(IMemoryCache cache) : SaveChangesInterceptor {
    private static readonly Dictionary<Type, string> CacheKeys = new() {
        [typeof(NflSeasonWeekConfig)] = ScheduleCache.NflWeekConfigs,
        [typeof(CfbSlates)] = ScheduleCache.CfbSlates,
        [typeof(CfbSeasonWeekConfig)] = ScheduleCache.CfbWeekConfigs,
    };

    // Which cache keys each in-flight save touched, recorded before the save and evicted after it.
    private readonly ConditionalWeakTable<DbContext, HashSet<string>> _pending = new();

    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result) {
        Record(eventData.Context);
        return result;
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData,
        InterceptionResult<int> result, CancellationToken cancellationToken = default) {
        Record(eventData.Context);
        return ValueTask.FromResult(result);
    }

    public override int SavedChanges(SaveChangesCompletedEventData eventData, int result) {
        Evict(eventData.Context);
        return result;
    }

    public override ValueTask<int> SavedChangesAsync(SaveChangesCompletedEventData eventData, int result,
        CancellationToken cancellationToken = default) {
        Evict(eventData.Context);
        return ValueTask.FromResult(result);
    }

    public override void SaveChangesFailed(DbContextErrorEventData eventData) => Forget(eventData.Context);

    public override Task SaveChangesFailedAsync(DbContextErrorEventData eventData, CancellationToken cancellationToken = default) {
        Forget(eventData.Context);
        return Task.CompletedTask;
    }

    private void Record(DbContext? context) {
        if (context is null) return;
        var keys = context.ChangeTracker.Entries()
            .Where(e => e.State is EntityState.Added or EntityState.Modified or EntityState.Deleted)
            .Select(e => CacheKeys.GetValueOrDefault(e.Metadata.ClrType))
            .OfType<string>()
            .ToHashSet();
        if (keys.Count > 0) _pending.AddOrUpdate(context, keys);
    }

    private void Evict(DbContext? context) {
        if (context is null || !_pending.TryGetValue(context, out var keys)) return;
        _pending.Remove(context);
        foreach (var key in keys) ScheduleCache.Invalidate(cache, key);
    }

    private void Forget(DbContext? context) {
        if (context is not null) _pending.Remove(context);
    }
}
