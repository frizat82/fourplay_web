using FourPlayWebApp.Server.Services;
using FourPlayWebApp.Shared.Models;
using Microsoft.Extensions.Caching.Memory;

namespace FourPlayWebApp.Server.UnitTests;

// ESPN only answers single-day scoreboard queries now, so a week/slate window is one request per
// day — and the pollers re-fetch the window every 30s during games and every 5 min otherwise. A
// day only changes while it has games still to finish, so each day's response is cached for as
// long as it can't change. NFL and CFB share this cache; the ESPN-reading jobs bypass it.
public class EspnDayCacheTests {
    private static readonly DateTimeOffset Now = new(2026, 9, 27, 18, 0, 0, TimeSpan.Zero);

    internal static Event Game(DateTimeOffset kickoff, TypeName status) => new() {
        Id = kickoff.Ticks.ToString() + status,
        Date = kickoff,
        Competitions = [new Competition {
            Date = kickoff,
            Competitors = [],
            Status = new EspnStatus { Type = new StatusType {
                Name = status,
                Completed = status == TypeName.StatusFinal,
                Description = status switch {
                    TypeName.StatusFinal => Description.Final,
                    TypeName.StatusScheduled => Description.Scheduled,
                    _ => Description.InProgress,
                },
            } },
        }],
    };

    internal static EspnScores Day(params Event[] games) => new() { Events = games };

    [Fact]
    public void ADayThatFinishedLongAgo_IsCachedForHours() =>
        Assert.Equal(EspnDayCache.SettledTtl, EspnDayCache.TtlFor(Day(Game(Now.AddHours(-20), TypeName.StatusFinal)), Now));

    // ESPN sometimes corrects a score after marking the game final; a day that only just finished
    // keeps getting re-checked for a while.
    [Fact]
    public void ADayThatJustFinished_IsRecheckedForCorrections() =>
        Assert.Equal(EspnDayCache.RecentlyFinishedTtl,
            EspnDayCache.TtlFor(Day(Game(Now.AddHours(-8), TypeName.StatusFinal), Game(Now.AddHours(-4), TypeName.StatusFinal)), Now));

    [Fact]
    public void ADayWithAGameOn_IsCachedForLessThanOneFastPoll() {
        var day = Day(Game(Now.AddHours(-5), TypeName.StatusFinal), Game(Now.AddHours(-1), TypeName.StatusInProgress));
        Assert.Equal(EspnDayCache.LiveTtl, EspnDayCache.TtlFor(day, Now));
        Assert.True(EspnDayCache.LiveTtl < EspnPollCadence.FastPollInterval);
    }

    [Fact]
    public void ADayOfUpcomingGames_IsCachedUntilItsFirstKickoff_Capped() {
        Assert.Equal(TimeSpan.FromHours(2),
            EspnDayCache.TtlFor(Day(Game(Now.AddHours(2), TypeName.StatusScheduled), Game(Now.AddHours(5), TypeName.StatusScheduled)), Now));
        Assert.Equal(EspnDayCache.SettledTtl, EspnDayCache.TtlFor(Day(Game(Now.AddDays(3), TypeName.StatusScheduled)), Now));
    }

    // Kickoff time has passed but ESPN still says scheduled: a delayed start is live...
    [Fact]
    public void AScheduledGameJustPastItsKickoff_CountsAsLive() =>
        Assert.Equal(EspnDayCache.LiveTtl, EspnDayCache.TtlFor(Day(Game(Now.AddMinutes(-5), TypeName.StatusScheduled)), Now));

    // ...but a postponed/canceled game (ESPN keeps those "scheduled") hours past kickoff isn't —
    // otherwise its whole day would be refetched every poll for the rest of the season.
    [Fact]
    public void AGameStillScheduledLongAfterKickoff_DoesNotKeepItsDayLive() =>
        Assert.Equal(EspnDayCache.SettledTtl,
            EspnDayCache.TtlFor(Day(Game(Now.AddHours(-20), TypeName.StatusFinal), Game(Now.AddHours(-20), TypeName.StatusScheduled)), Now));

    [Fact]
    public void ADayWithNoGames_IsRecheckedEveryFewPolls() =>
        Assert.Equal(EspnDayCache.EmptyTtl, EspnDayCache.TtlFor(Day(), Now));

    [Fact]
    public async Task GetOrFetchAsync_ServesACachedDay_AndNeverCachesAFailedOrMalformedFetch() {
        var cache = new EspnDayCache(new MemoryCache(new MemoryCacheOptions()), new FakeTimeProvider(Now));
        var calls = 0;
        Task<EspnScores?> Fetch() { calls++; return Task.FromResult<EspnScores?>(Day(Game(Now.AddHours(-20), TypeName.StatusFinal))); }
        Task<EspnScores?> Fail() { calls++; return Task.FromResult<EspnScores?>(null); }
        Task<EspnScores?> Malformed() { calls++; return Task.FromResult<EspnScores?>(new EspnScores()); } // a {} body: Events null

        await cache.GetOrFetchAsync("nfl/20260927", Fetch);
        await cache.GetOrFetchAsync("nfl/20260927", Fetch);
        Assert.Equal(1, calls);

        await cache.GetOrFetchAsync("nfl/20260928", Fail);
        await cache.GetOrFetchAsync("nfl/20260928", Fail);
        await cache.GetOrFetchAsync("nfl/20260929", Malformed);
        await cache.GetOrFetchAsync("nfl/20260929", Malformed);
        Assert.Equal(5, calls);
    }

    // The ESPN-reading jobs (scores, spreads, rankings) run a few times a week and persist what they
    // read, so they always fetch fresh — and refresh the cache for the pollers while they're at it.
    [Fact]
    public async Task InsideAFreshScope_AlwaysFetches_AndRefreshesTheCache() {
        var cache = new EspnDayCache(new MemoryCache(new MemoryCacheOptions()), new FakeTimeProvider(Now));
        var version = 0;
        Task<EspnScores?> Fetch() { version++; return Task.FromResult<EspnScores?>(Day(Game(Now.AddHours(-20 - version), TypeName.StatusFinal))); }

        var first = await cache.GetOrFetchAsync("cfb/20260926", Fetch);
        EspnScores? fresh;
        using (EspnDayCache.Fresh()) fresh = await cache.GetOrFetchAsync("cfb/20260926", Fetch);
        var afterwards = await cache.GetOrFetchAsync("cfb/20260926", Fetch);

        Assert.Equal(2, version);
        Assert.NotSame(first, fresh);
        Assert.Same(fresh, afterwards);
    }
}
