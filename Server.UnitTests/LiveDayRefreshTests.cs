using FourPlayWebApp.Server.Services;
using FourPlayWebApp.Shared.Models;

namespace FourPlayWebApp.Server.UnitTests;

// During games the NFL and CFB pollers refresh every 30s. A game only changes on the day it's
// played, so a live poll fetches just the ESPN day bucket(s) of games in their live window and
// swaps those games into the week's scoreboard we already hold — 1-2 ESPN requests instead of one
// per day of the whole week (8 for NFL, 7 for CFB). Anything else is a full-window fetch.
public class LiveDayRefreshTests {
    // Sunday 2026-09-27, 1pm ET kickoff = 17:00 UTC.
    private static readonly DateTimeOffset SundayEarly = new(2026, 9, 27, 17, 0, 0, TimeSpan.Zero);
    // Thursday 2026-09-24, 8:15pm ET kickoff = 00:15 UTC Friday.
    private static readonly DateTimeOffset ThursdayNight = new(2026, 9, 25, 0, 15, 0, TimeSpan.Zero);

    private static Event Game(string id, DateTimeOffset kickoff, int homeScore = 0) => new() {
        Id = id,
        Date = kickoff,
        Competitions = [new Competition {
            Id = id,
            Date = kickoff,
            Competitors = [
                new Competitor { HomeAway = HomeAway.Home, Score = homeScore, Team = new EspnTeam { Abbreviation = "H" + id }, Records = [] },
                new Competitor { HomeAway = HomeAway.Away, Score = 0, Team = new EspnTeam { Abbreviation = "A" + id }, Records = [] },
            ],
        }],
    };

    private static EspnScores Week(params Event[] events) => new() { Events = events };

    private static int HomeScore(EspnScores s, string id) =>
        (int)s.Events!.Single(e => e.Id == id).Competitions[0].Competitors.Single(c => c.HomeAway == HomeAway.Home).Score;

    [Fact]
    public void LiveDays_AreTheEasternAndUtcDatesOfGamesInTheirLiveWindow() {
        var week = Week(Game("thu", ThursdayNight), Game("sun", SundayEarly));

        // Thursday night game, 1h in: ET says Thu 24th, UTC says Fri 25th — ESPN's bucket could be either.
        Assert.Equal([new DateOnly(2026, 9, 24), new DateOnly(2026, 9, 25)],
            LiveDayRefresh.LiveDays(week, ThursdayNight.AddHours(1)).Order());
        // Sunday 1pm game, 2h in: both calendars agree.
        Assert.Equal([new DateOnly(2026, 9, 27)], LiveDayRefresh.LiveDays(week, SundayEarly.AddHours(2)));
        // Between games (Saturday): nothing live.
        Assert.Empty(LiveDayRefresh.LiveDays(week, SundayEarly.AddDays(-1)));
    }

    [Fact]
    public async Task LivePoll_FetchesOnlyTheLiveDays_AndKeepsTheRestOfTheWeek() {
        var previous = Week(Game("thu", ThursdayNight, homeScore: 24), Game("sun1", SundayEarly, 3), Game("sun2", SundayEarly, 0));
        IReadOnlyCollection<DateOnly>? requested = null;
        var fullFetches = 0;

        var result = await LiveDayRefresh.FetchAsync(previous, SundayEarly.AddHours(1),
            days => { requested = days; return Task.FromResult<EspnScores?>(Week(Game("sun1", SundayEarly, 10), Game("sun2", SundayEarly, 7))); },
            () => { fullFetches++; return Task.FromResult<EspnScores?>(null); });

        Assert.Equal([new DateOnly(2026, 9, 27)], requested);
        Assert.Equal(0, fullFetches);
        Assert.Equal(["thu", "sun1", "sun2"], result!.Events!.Select(e => e.Id)); // order and other days kept
        Assert.Equal(24, HomeScore(result, "thu"));
        Assert.Equal(10, HomeScore(result, "sun1"));
        Assert.Equal(7, HomeScore(result, "sun2"));
    }

    // The week's game list comes from the full-window fetch, which applies the week's own date
    // filter; a live poll only refreshes games already in it.
    [Fact]
    public async Task LivePoll_DoesNotAddGamesThatWerentAlreadyInTheWeek() {
        var previous = Week(Game("sun1", SundayEarly));
        var result = await LiveDayRefresh.FetchAsync(previous, SundayEarly.AddHours(1),
            _ => Task.FromResult<EspnScores?>(Week(Game("sun1", SundayEarly, 3), Game("other", SundayEarly))),
            () => Task.FromResult<EspnScores?>(null));

        Assert.Equal(["sun1"], result!.Events!.Select(e => e.Id));
    }

    [Fact]
    public async Task FullFetch_WhenThereIsNoPreviousScoreboard_OrNothingIsLive_OrTheDayFetchFails() {
        var full = Week(Game("full", SundayEarly));
        var dayFetches = 0;
        Task<EspnScores?> Days(IReadOnlyCollection<DateOnly> _) { dayFetches++; return Task.FromResult<EspnScores?>(null); }
        Task<EspnScores?> Full() => Task.FromResult<EspnScores?>(full);

        Assert.Same(full, await LiveDayRefresh.FetchAsync(null, SundayEarly, Days, Full));
        Assert.Equal(0, dayFetches);

        Assert.Same(full, await LiveDayRefresh.FetchAsync(Week(Game("sun1", SundayEarly)), SundayEarly.AddDays(-1), Days, Full));
        Assert.Equal(0, dayFetches);

        // Live, but ESPN returned nothing for the day: fall back to the full fetch rather than
        // serving (or clearing) a stale scoreboard.
        Assert.Same(full, await LiveDayRefresh.FetchAsync(Week(Game("sun1", SundayEarly)), SundayEarly.AddHours(1), Days, Full));
        Assert.Equal(1, dayFetches);
    }
}
