using FourPlayWebApp.Server.Models.Data;
using FourPlayWebApp.Server.Services.Repositories;
using Microsoft.Extensions.Caching.Memory;

namespace FourPlayWebApp.Server.UnitTests;

// "What week/slate is it" is resolved from the schedule tables (NflSeasonWeekConfigs, CfbSlates,
// CfbSeasonWeekConfigs) on every poll and most requests — ~5k full-table reads a day that also
// kept Neon from ever suspending. The rows change a few times a season, so the repositories cache
// them and their own writers evict.
public class ScheduleCacheTests {
    private static CfbSlates Slate(int id, int season, int number) =>
        new() { Id = id, Season = season, SlateNumber = number, Label = $"W{number}", SlateType = "RegularSeason" };

    private static CfbSeasonWeekConfig WeekConfig(int id, int season, int espnWeek) =>
        new() { Id = id, Season = season, EspnWeekNumber = espnWeek, IvLeagueWeekNumber = espnWeek, ScoringFormat = "Standard" };

    [Fact]
    public async Task CfbSchedule_IsReadOnce_ThenServedFromCache_IncludingPerSeasonAndByIdReads() {
        var factory = new DbContextFactoryStub(nameof(CfbSchedule_IsReadOnce_ThenServedFromCache_IncludingPerSeasonAndByIdReads));
        var db = factory.CreateDbContext();
        db.CfbSlates.AddRange(Slate(1, 2026, 1), Slate(2, 2026, 2), Slate(3, 2025, 1));
        db.CfbSeasonWeekConfigs.Add(WeekConfig(1, 2026, 1));
        await db.SaveChangesAsync();
        var repo = new CfbRepository(factory, new MemoryCache(new MemoryCacheOptions()));

        await repo.GetAllSlatesAsync();
        await repo.GetAllWeekConfigsAsync();
        // A row that appears without going through the repository isn't seen until the cache
        // expires — proof the reads below never touch the database.
        db.CfbSlates.Add(Slate(4, 2026, 3));
        db.CfbSeasonWeekConfigs.Add(WeekConfig(2, 2026, 2));
        await db.SaveChangesAsync();

        Assert.Equal([3, 1, 2], (await repo.GetAllSlatesAsync()).Select(s => s.Id)); // Season, SlateNumber order
        Assert.Equal([1, 2], (await repo.GetSlatesForSeasonAsync(2026)).Select(s => s.Id));
        Assert.Equal(2, (await repo.GetSlateByIdAsync(2))!.SlateNumber);
        Assert.Null(await repo.GetSlateByIdAsync(4));
        Assert.Equal([1], (await repo.GetAllWeekConfigsAsync()).Select(c => c.Id));
        Assert.Equal([1], (await repo.GetWeekConfigsForSeasonAsync(2026)).Select(c => c.Id));
    }

    [Fact]
    public async Task CfbSchedule_WritersEvict() {
        var factory = new DbContextFactoryStub(nameof(CfbSchedule_WritersEvict));
        var repo = new CfbRepository(factory, new MemoryCache(new MemoryCacheOptions()));

        Assert.Empty(await repo.GetAllSlatesAsync());
        var second = Slate(2, 2026, 2); // the stub shares one DbContext, so delete the tracked instance
        await repo.AddSlatesAsync([Slate(1, 2026, 1), second]);
        Assert.Equal([1, 2], (await repo.GetAllSlatesAsync()).Select(s => s.Id));

        await repo.DeleteSlatesAsync([second]);
        Assert.Equal([1], (await repo.GetAllSlatesAsync()).Select(s => s.Id));

        Assert.Empty(await repo.GetAllWeekConfigsAsync());
        await repo.AddWeekConfigsAsync([WeekConfig(1, 2026, 1)]);
        Assert.Single(await repo.GetAllWeekConfigsAsync());
    }

    // Callers get their own list AND their own row objects: one caller sorting a list or tweaking a
    // row can't change what every other request and poller sees for the next hour.
    [Fact]
    public async Task CachedRows_AreCopies() {
        var factory = new DbContextFactoryStub(nameof(CachedRows_AreCopies));
        var repo = new LeagueRepository(factory, new MemoryCache(new MemoryCacheOptions()));
        var db = factory.CreateDbContext();
        db.NflSeasonWeekConfigs.AddRange(
            new NflSeasonWeekConfig { Id = 1, Season = 2026, WeekId = 1, WeekLabel = "Week 1", WeekType = "RegularSeason", ScoringFormat = "Standard" },
            new NflSeasonWeekConfig { Id = 2, Season = 2026, WeekId = 2, WeekLabel = "Week 2", WeekType = "RegularSeason", ScoringFormat = "Standard" });
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
        db.NflSeasonWeekConfigs.Add(new NflSeasonWeekConfig { Id = 1, Season = 2026, WeekId = 1, WeekLabel = "Week 1", WeekType = "RegularSeason", ScoringFormat = "Standard" });
        await db.SaveChangesAsync();

        Assert.Empty(await repo.GetNflSeasonWeekConfigsAsync());
    }

    // DemoDataSeeder writes these tables directly (and UserManagerJob re-runs it after the app is
    // serving), so it evicts everything when it's done.
    [Fact]
    public async Task InvalidateAll_EvictsEveryScheduleTable() {
        var factory = new DbContextFactoryStub(nameof(InvalidateAll_EvictsEveryScheduleTable));
        var cache = new MemoryCache(new MemoryCacheOptions());
        var cfb = new CfbRepository(factory, cache);
        var nfl = new LeagueRepository(factory, cache);
        await cfb.GetAllSlatesAsync(); await cfb.GetAllWeekConfigsAsync(); await nfl.GetNflSeasonWeekConfigsAsync();

        var db = factory.CreateDbContext();
        db.CfbSlates.Add(Slate(1, 2026, 1));
        db.CfbSeasonWeekConfigs.Add(WeekConfig(1, 2026, 1));
        db.NflSeasonWeekConfigs.Add(new NflSeasonWeekConfig { Id = 1, Season = 2026, WeekId = 1, WeekLabel = "Week 1", WeekType = "RegularSeason", ScoringFormat = "Standard" });
        await db.SaveChangesAsync();
        FourPlayWebApp.Server.Services.ScheduleCache.InvalidateAll(cache);

        Assert.Single(await cfb.GetAllSlatesAsync());
        Assert.Single(await cfb.GetAllWeekConfigsAsync());
        Assert.Single(await nfl.GetNflSeasonWeekConfigsAsync());
    }
}
