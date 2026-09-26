using FourPlayWebApp.Server.Services;
using FourPlayWebApp.Shared.Models;

namespace FourPlayWebApp.Server.UnitTests;

// During games the NFL and CFB pollers refresh every 30s. A game only changes on the day it's
// played, so a live poll fetches just the ESPN day bucket of games in their live window and swaps
// those games into the week's scoreboard already held — 1 ESPN request instead of one per day of
// the whole week (8 for NFL, 7 for CFB). PolledItemSnapshot decides when a full-window fetch is
// due instead (see PolledItemSnapshotTests).
public class LiveDayRefreshTests {
    // Sunday 2026-09-27, 1pm ET kickoff = 17:00 UTC.
    private static readonly DateTimeOffset SundayEarly = new(2026, 9, 27, 17, 0, 0, TimeSpan.Zero);
    // Thursday 2026-09-24, 8:15pm ET kickoff = 00:15 UTC Friday.
    private static readonly DateTimeOffset ThursdayNight = new(2026, 9, 25, 0, 15, 0, TimeSpan.Zero);

    internal static Event Game(string id, DateTimeOffset kickoff, int homeScore = 0) => new() {
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

    internal static EspnScores Week(params Event[] events) => new() { Events = events };

    internal static int HomeScore(EspnScores s, string id) =>
        (int)s.Events!.Single(e => e.Id == id).Competitions[0].Competitors.Single(c => c.HomeAway == HomeAway.Home).Score;

    private static Func<IReadOnlyCollection<DateOnly>, Task<EspnScores?>> Days(
        List<IReadOnlyCollection<DateOnly>> calls, Func<IReadOnlyCollection<DateOnly>, EspnScores?> respond) =>
        days => { calls.Add(days); return Task.FromResult(respond(days)); };

    [Fact]
    public async Task FetchesOnlyTheEasternDayOfLiveGames_AndKeepsTheRestOfTheWeek() {
        var previous = Week(Game("thu", ThursdayNight, homeScore: 24), Game("sun1", SundayEarly, 3), Game("sun2", SundayEarly, 0));
        var calls = new List<IReadOnlyCollection<DateOnly>>();

        var result = await LiveDayRefresh.TryMergeLiveDaysAsync(previous, SundayEarly.AddHours(1),
            Days(calls, _ => Week(Game("sun1", SundayEarly, 10), Game("sun2", SundayEarly, 7))));

        Assert.Equal([[new DateOnly(2026, 9, 27)]], calls);
        Assert.Equal(["thu", "sun1", "sun2"], result!.Events!.Select(e => e.Id)); // order and other days kept
        Assert.Equal(24, HomeScore(result, "thu"));
        Assert.Equal(10, HomeScore(result, "sun1"));
        Assert.Equal(7, HomeScore(result, "sun2"));
    }

    // A late game (8:15pm ET = 00:15 UTC next day) is in ESPN's Eastern-date bucket, so normally
    // that's the only request. If ESPN ever returns it under the UTC date instead, the live game
    // must not silently stop updating: fetch the UTC day for just the games that were missing.
    [Fact]
    public async Task LateGame_IsFetchedFromTheEasternDay_FallingBackToTheUtcDayOnlyIfMissing() {
        var previous = Week(Game("thu", ThursdayNight));
        var now = ThursdayNight.AddHours(1);

        var calls = new List<IReadOnlyCollection<DateOnly>>();
        var found = await LiveDayRefresh.TryMergeLiveDaysAsync(previous, now, Days(calls, _ => Week(Game("thu", ThursdayNight, 7))));
        Assert.Equal([[new DateOnly(2026, 9, 24)]], calls);
        Assert.Equal(7, HomeScore(found!, "thu"));

        calls.Clear();
        var fallback = await LiveDayRefresh.TryMergeLiveDaysAsync(previous, now, Days(calls, days =>
            days.Contains(new DateOnly(2026, 9, 25)) ? Week(Game("thu", ThursdayNight, 14)) : Week()));
        Assert.Equal([[new DateOnly(2026, 9, 24)], [new DateOnly(2026, 9, 25)]], calls);
        Assert.Equal(14, HomeScore(fallback!, "thu"));
    }

    // The week's game list comes from the full-window fetch, which applies the week's own date
    // filter; a live poll only refreshes games already in it.
    [Fact]
    public async Task DoesNotAddGamesThatWerentAlreadyInTheWeek() {
        var result = await LiveDayRefresh.TryMergeLiveDaysAsync(Week(Game("sun1", SundayEarly)), SundayEarly.AddHours(1),
            _ => Task.FromResult<EspnScores?>(Week(Game("sun1", SundayEarly, 3), Game("other", SundayEarly))));

        Assert.Equal(["sun1"], result!.Events!.Select(e => e.Id));
    }

    // null = "do a full fetch instead".
    [Fact]
    public async Task ReturnsNull_WhenNothingIsLive_OrTheDayFetchReturnsNothing() {
        var calls = new List<IReadOnlyCollection<DateOnly>>();
        var week = Week(Game("sun1", SundayEarly));

        Assert.Null(await LiveDayRefresh.TryMergeLiveDaysAsync(week, SundayEarly.AddDays(-1), Days(calls, _ => week)));
        Assert.Empty(calls);

        Assert.Null(await LiveDayRefresh.TryMergeLiveDaysAsync(week, SundayEarly.AddHours(1), Days(calls, _ => null)));
    }
}
