using FourPlayWebApp.Server.Services;
using FourPlayWebApp.Shared.Models;
using Microsoft.Extensions.Caching.Memory;

namespace FourPlayWebApp.Server.UnitTests;

// ESPN only answers single-day scoreboard queries now, so a week/slate window is one request per
// day — and the pollers re-fetch the window every 30s during games and every 5 min otherwise. A
// day only changes while it has games still to finish, so each day's response is cached for as
// long as it can't change: finished days for hours, days with a game on for less than one fast
// poll, days of upcoming games until their first kickoff. NFL and CFB share this cache.
public class EspnDayCacheTests {
    private static readonly DateTimeOffset Now = new(2026, 9, 27, 18, 0, 0, TimeSpan.Zero);

    private static Event Game(DateTimeOffset kickoff, TypeName status) => new() {
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

    private static EspnScores Day(params Event[] games) => new() { Events = games };

    [Fact]
    public void ADayOfFinishedGames_IsCachedForHours() =>
        Assert.Equal(EspnDayCache.SettledTtl,
            EspnDayCache.TtlFor(Day(Game(Now.AddHours(-5), TypeName.StatusFinal), Game(Now.AddHours(-8), TypeName.StatusFinal)), Now));

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
        Assert.Equal(EspnDayCache.SettledTtl,
            EspnDayCache.TtlFor(Day(Game(Now.AddDays(3), TypeName.StatusScheduled)), Now));
    }

    // Kickoff time has passed but ESPN still says scheduled (delayed start): treat it as live.
    [Fact]
    public void AScheduledGamePastItsKickoff_CountsAsLive() =>
        Assert.Equal(EspnDayCache.LiveTtl, EspnDayCache.TtlFor(Day(Game(Now.AddMinutes(-5), TypeName.StatusScheduled)), Now));

    [Fact]
    public void ADayWithNoGames_IsCachedForAnHour() =>
        Assert.Equal(TimeSpan.FromHours(1), EspnDayCache.TtlFor(Day(), Now));

    [Fact]
    public async Task GetOrFetchAsync_ServesACachedDay_AndNeverCachesAFailedFetch() {
        var cache = new EspnDayCache(new MemoryCache(new MemoryCacheOptions()), new FakeTimeProvider(Now));
        var calls = 0;
        Task<EspnScores?> Fetch() { calls++; return Task.FromResult<EspnScores?>(Day(Game(Now.AddHours(-5), TypeName.StatusFinal))); }
        Task<EspnScores?> Fail() { calls++; return Task.FromResult<EspnScores?>(null); }

        await cache.GetOrFetchAsync("nfl/20260927", Fetch);
        await cache.GetOrFetchAsync("nfl/20260927", Fetch);
        Assert.Equal(1, calls);

        Assert.Null(await cache.GetOrFetchAsync("nfl/20260928", Fail));
        await cache.GetOrFetchAsync("nfl/20260928", Fail);
        Assert.Equal(3, calls);
    }
}
