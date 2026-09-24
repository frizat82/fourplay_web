using FourPlayWebApp.Server.Models.Data;
using FourPlayWebApp.Server.Services;
using FourPlayWebApp.Server.Services.Repositories;
using FourPlayWebApp.Server.Services.Repositories.Interfaces;
using FourPlayWebApp.Shared.Models.Data;
using Microsoft.Extensions.Caching.Memory;
using NSubstitute;

namespace FourPlayWebApp.Server.UnitTests;

// Spreads and the leaderboard are derived from a league's per-season settings (LeagueJuiceMapping),
// cached for up to an hour. Those caches must be keyed by (league, season), dropped by EVERY writer
// of the settings (not just the endpoints that happened to remember to), and immune to a read that
// was already in flight when the settings changed.
public class LeagueSettingsCacheTests {
    private static List<NflSpreads> Week1(int season) => [
        new() { Season = season, NflWeek = 1, HomeTeam = "KC", AwayTeam = "BAL", HomeTeamSpread = -3, AwayTeamSpread = 3, OverUnder = 45 },
    ];

    [Fact]
    public async Task Provider_UsesEachSeasonsOwnJuice_WhenTheCacheIsShared() {
        var repo = Substitute.For<ILeagueRepository>();
        repo.GetNflSpreadsAsync(2024, 1).Returns(Week1(2024));
        repo.GetNflSpreadsAsync(2025, 1).Returns(Week1(2025));
        repo.GetLeagueJuiceMappingAsync(1, 2024).Returns(new LeagueJuiceMapping { LeagueId = 1, Season = 2024, Juice = 13 });
        repo.GetLeagueJuiceMappingAsync(1, 2025).Returns(new LeagueJuiceMapping { LeagueId = 1, Season = 2025, Juice = 7 });
        var provider = new SpreadCalculatorProvider(repo, new MemoryCache(new MemoryCacheOptions()));

        var past = await provider.GetForNflWeekAsync(1, 2024, 1);
        var current = await provider.GetForNflWeekAsync(1, 2025, 1);

        Assert.Equal((double?)(-3 + 13), past.GetSpread("KC"));
        Assert.Equal((double?)(-3 + 7), current.GetSpread("KC"));
    }

    // Covers every writer at once — commissioner save, roll-forward, admin add, LeagueJuiceLockJob,
    // CreateLeague — because they all go through these two repository methods.
    [Fact]
    public async Task Repository_UpdateAndAdd_EvictThatSeasonsCachedJuiceAndLeaderboard() {
        var factory = new DbContextFactoryStub(nameof(Repository_UpdateAndAdd_EvictThatSeasonsCachedJuiceAndLeaderboard));
        var cache = new MemoryCache(new MemoryCacheOptions());
        var repo = new LeagueRepository(factory, cache);
        var mapping = new LeagueJuiceMapping { LeagueId = 1, Season = 2025, Juice = 13 };
        await repo.AddLeagueJuiceMappingAsync(mapping);

        void Prime(int season) {
            cache.Set(LeagueCacheKeys.Juice(1, season), new LeagueJuiceMapping());
            cache.Set(LeagueCacheKeys.Leaderboard(1, season), new List<LeaderboardModel>());
        }
        bool Cached(int season) => cache.TryGetValue(LeagueCacheKeys.Juice(1, season), out _)
            || cache.TryGetValue(LeagueCacheKeys.Leaderboard(1, season), out _);

        Prime(2025);
        Prime(2024);
        await repo.UpdateLeagueJuiceMappingAsync(new LeagueJuiceMapping { Id = mapping.Id, LeagueId = 1, Season = 2025, Juice = 9 });
        Assert.False(Cached(2025));
        Assert.True(Cached(2024)); // other seasons untouched

        Prime(2026);
        await repo.AddLeagueJuiceMappingAsync(new LeagueJuiceMapping { LeagueId = 1, Season = 2026, Juice = 13 });
        Assert.False(Cached(2026));
    }

    // A read that started before the settings changed must not re-cache the old value after the
    // eviction (it would otherwise be served for the full hour).
    [Fact]
    public async Task Provider_ReadInFlightDuringInvalidation_DoesNotCacheTheStaleJuice() {
        var repo = Substitute.For<ILeagueRepository>();
        repo.GetNflSpreadsAsync(2025, 1).Returns(Week1(2025));
        var inFlight = new TaskCompletionSource<LeagueJuiceMapping?>();
        repo.GetLeagueJuiceMappingAsync(1, 2025).Returns(inFlight.Task, Task.FromResult<LeagueJuiceMapping?>(new LeagueJuiceMapping { Juice = 9 }));
        var cache = new MemoryCache(new MemoryCacheOptions());
        var provider = new SpreadCalculatorProvider(repo, cache);

        var stale = provider.GetForNflWeekAsync(1, 2025, 1);        // reads juice... still waiting
        LeagueCacheKeys.InvalidateLeagueSeason(cache, 1, 2025);     // settings saved meanwhile
        inFlight.SetResult(new LeagueJuiceMapping { Juice = 13 });  // the old value arrives late
        await stale;

        var fresh = await provider.GetForNflWeekAsync(1, 2025, 1);
        Assert.Equal((double?)(-3 + 9), fresh.GetSpread("KC"));
    }
}
