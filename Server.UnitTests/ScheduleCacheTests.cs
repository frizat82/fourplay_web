using FourPlayWebApp.Shared.Models.Enum;
using FourPlayWebApp.Server.Data;
using FourPlayWebApp.Server.Models.Data;
using FourPlayWebApp.Server.Services;
using FourPlayWebApp.Server.Services.Repositories;
using Microsoft.Extensions.Caching.Memory;

namespace FourPlayWebApp.Server.UnitTests;

// "What week/slate is it" is resolved from the control tables (NflSeasonWeekConfigs,
// CfbSeasonWeekConfigs) on every poll and most requests — ~5k full-table reads a day that also
// kept Neon from ever suspending. The rows change a few times a season, so the repositories cache
// them, and any save that touches one of those tables evicts it (ScheduleCacheInterceptor — covers
// the repositories, DemoDataSeeder's direct writes, and any future writer).
public class ScheduleCacheTests {
    private static CfbSeasonWeekConfig Slate(int id, int season, int number, bool inScope = true) =>
        new() { Id = id, Season = season, IvLeagueWeekNumber = inScope ? number : null, InScopeIvLeague = inScope,
            WeekLabel = $"W{number}", WeekStartDate = new DateOnly(season, 9, 1).AddDays(7 * number) };

    [Fact]
    public async Task CfbSchedule_IsReadOnce_ThenServedFromCache_IncludingPerSeasonAndByIdReads() {
        var factory = new DbContextFactoryStub(nameof(CfbSchedule_IsReadOnce_ThenServedFromCache_IncludingPerSeasonAndByIdReads));
        var db = factory.CreateDbContext();
        // frizat-b4g: a slate is an in-scope row of the one CFB control table; a skipped week is a
        // row too, but never a slate.
        db.CfbSeasonWeekConfigs.AddRange(Slate(1, 2026, 1), Slate(2, 2026, 2), Slate(3, 2025, 1), Slate(5, 2026, 0, inScope: false));
        await db.SaveChangesAsync();
        var repo = new CfbRepository(factory, new MemoryCache(new MemoryCacheOptions()));

        await repo.GetAllSlatesAsync();
        // A row that appears without going through the repository isn't seen until the cache
        // expires — proof the reads below never touch the database.
        db.CfbSeasonWeekConfigs.Add(Slate(4, 2026, 3));
        await db.SaveChangesAsync();

        Assert.Equal([3, 1, 2], (await repo.GetAllSlatesAsync()).Select(s => s.Id)); // Season, week number order
        Assert.Equal([1, 2], (await repo.GetSlatesForSeasonAsync(2026)).Select(s => s.Id));
        Assert.Equal(2, (await repo.GetSlateByIdAsync(2))!.SlateNumber);
        Assert.Null(await repo.GetSlateByIdAsync(4));
        Assert.Null(await repo.GetSlateByIdAsync(5)); // skipped week
    }

    [Fact]
    public async Task CfbSchedule_WritesThroughTheRepository_Evict() {
        var cache = new MemoryCache(new MemoryCacheOptions());
        var factory = new DbContextFactoryStub(nameof(CfbSchedule_WritesThroughTheRepository_Evict), new ScheduleCacheInterceptor(cache));
        var repo = new CfbRepository(factory, cache);

        Assert.Empty(await repo.GetAllSlatesAsync());
        await repo.AddWeekConfigsAsync([Slate(1, 2026, 1), Slate(2, 2026, 2)]);
        Assert.Equal([1, 2], (await repo.GetAllSlatesAsync()).Select(s => s.Id));
    }

    // Callers get their own list AND their own row objects: one caller sorting a list or tweaking a
    // row can't change what every other request and poller sees for the next hour.
    [Fact]
    public async Task CachedRows_AreCopies() {
        var factory = new DbContextFactoryStub(nameof(CachedRows_AreCopies));
        var repo = new LeagueRepository(factory, new MemoryCache(new MemoryCacheOptions()));
        var db = factory.CreateDbContext();
        db.NflSeasonWeekConfigs.AddRange(
            new NflSeasonWeekConfig { Id = 1, Season = 2026, WeekId = 1, WeekLabel = "Week 1", WeekType = WeekType.RegularSeason, ScoringFormat = ScoringFormat.Standard },
            new NflSeasonWeekConfig { Id = 2, Season = 2026, WeekId = 2, WeekLabel = "Week 2", WeekType = WeekType.RegularSeason, ScoringFormat = ScoringFormat.Standard });
        await db.SaveChangesAsync();

        var first = await repo.GetNflSeasonWeekConfigsAsync();
        first[0].WeekLabel = "changed by a caller";
        first.Clear();

        Assert.Equal(2, (await repo.GetNflSeasonWeekConfigsAsync()).Count);
        Assert.Equal("Week 1", (await repo.GetNflSeasonWeekConfigsAsync())[0].WeekLabel);
        Assert.Equal([1, 2], (await repo.GetNflSeasonWeekConfigsAsync(2026)).Select(c => c.WeekId));
    }

    [Fact]
    public async Task NflWeekConfigs_AreServedFromCache() {
        var factory = new DbContextFactoryStub(nameof(NflWeekConfigs_AreServedFromCache));
        var repo = new LeagueRepository(factory, new MemoryCache(new MemoryCacheOptions()));
        Assert.Empty(await repo.GetNflSeasonWeekConfigsAsync());

        var db = factory.CreateDbContext();
        db.NflSeasonWeekConfigs.Add(new NflSeasonWeekConfig { Id = 1, Season = 2026, WeekId = 1, WeekLabel = "Week 1", WeekType = WeekType.RegularSeason, ScoringFormat = ScoringFormat.Standard });
        await db.SaveChangesAsync();

        Assert.Empty(await repo.GetNflSeasonWeekConfigsAsync());
    }

    // DemoDataSeeder writes these tables straight through its DbContext (and UserManagerJob re-runs
    // it after the app is serving) — any save touching a schedule table evicts that table, whoever
    // made it; other tables' saves leave the cache alone.
    [Fact]
    public async Task AnySaveTouchingAScheduleTable_EvictsThatTable() {
        var cache = new MemoryCache(new MemoryCacheOptions());
        var factory = new DbContextFactoryStub(nameof(AnySaveTouchingAScheduleTable_EvictsThatTable), new ScheduleCacheInterceptor(cache));
        var cfb = new CfbRepository(factory, cache);
        var nfl = new LeagueRepository(factory, cache);
        await cfb.GetAllSlatesAsync(); await nfl.GetNflSeasonWeekConfigsAsync();

        var db = factory.CreateDbContext();
        db.NflSeasonWeekConfigs.Add(new NflSeasonWeekConfig { Id = 1, Season = 2026, WeekId = 1, WeekLabel = "Week 1", WeekType = WeekType.RegularSeason, ScoringFormat = ScoringFormat.Standard });
        await db.SaveChangesAsync();
        Assert.Single(await nfl.GetNflSeasonWeekConfigsAsync());
        Assert.True(cache.TryGetValue(ScheduleCache.CfbWeekConfigs, out _)); // untouched tables stay cached

        db.CfbSeasonWeekConfigs.Add(Slate(1, 2026, 1));
        db.SaveChanges(); // the synchronous path too
        Assert.Single(await cfb.GetAllSlatesAsync());
    }
}
