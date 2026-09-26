using FourPlayWebApp.Server.Services;
using FourPlayWebApp.Shared.Models;

namespace FourPlayWebApp.Server.UnitTests;

public class PolledItemSnapshotTests {
    [Fact]
    public void Serves_TheRecordedItem_OnlyForItsOwnKey() {
        var snapshot = new PolledItemSnapshot(TimeSpan.FromMinutes(10));
        var scores = new EspnScores();
        snapshot.Record("week-3", scores);

        Assert.True(snapshot.TryGet("week-3", out var hit));
        Assert.Same(scores, hit);
        Assert.False(snapshot.TryGet("week-2", out _));
    }

    // If the poller stops refreshing (ESPN outage mid-game, loop died), requests must fall back to
    // their own live fetch rather than be served the last poll's scores indefinitely.
    [Fact]
    public void StopsServing_ASnapshotOlderThanItsMaxAge() {
        var clock = new FakeTimeProvider(new DateTimeOffset(2026, 9, 20, 18, 0, 0, TimeSpan.Zero));
        var snapshot = new PolledItemSnapshot(TimeSpan.FromMinutes(10), clock);
        snapshot.Record("week-3", new EspnScores());

        clock.Advance(TimeSpan.FromMinutes(9));
        Assert.True(snapshot.TryGet("week-3", out _));

        clock.Advance(TimeSpan.FromMinutes(2));
        Assert.False(snapshot.TryGet("week-3", out _));
    }

    [Fact]
    public void Clear_And_NullRecord_StopServing() {
        var snapshot = new PolledItemSnapshot(TimeSpan.FromMinutes(10));
        snapshot.Record("week-3", new EspnScores());
        snapshot.Clear();
        Assert.False(snapshot.TryGet("week-3", out _));

        snapshot.Record("week-3", new EspnScores());
        snapshot.Record("week-3", null);
        Assert.False(snapshot.TryGet("week-3", out _));
    }

    // ── RefreshAsync: live-day merge vs full-window fetch ───────────────────────────────────────

    private static readonly DateTimeOffset Kickoff = new(2026, 9, 27, 17, 0, 0, TimeSpan.Zero);

    private sealed class Espn {
        public int Full, Day;
        public Task<EspnScores?> FetchAll() {
            Full++;
            return Task.FromResult<EspnScores?>(LiveDayRefreshTests.Week(LiveDayRefreshTests.Game("g", Kickoff, homeScore: Full * 100)));
        }
        public Task<EspnScores?> FetchDays(IReadOnlyCollection<DateOnly> _) {
            Day++;
            return Task.FromResult<EspnScores?>(LiveDayRefreshTests.Week(LiveDayRefreshTests.Game("g", Kickoff, homeScore: Day)));
        }
    }

    [Fact]
    public async Task RefreshAsync_FullFetchFirst_ThenOnlyLiveDays_WhileAGameIsLive() {
        var clock = new FakeTimeProvider(Kickoff.AddMinutes(10));
        var snapshot = new PolledItemSnapshot(TimeSpan.FromMinutes(10), clock);
        var espn = new Espn();

        await snapshot.RefreshAsync("week-3", espn.FetchDays, espn.FetchAll);
        clock.Advance(TimeSpan.FromSeconds(30));
        var second = await snapshot.RefreshAsync("week-3", espn.FetchDays, espn.FetchAll);

        Assert.Equal((1, 1), (espn.Full, espn.Day));
        Assert.Equal(1, LiveDayRefreshTests.HomeScore(second!, "g"));
        Assert.True(snapshot.TryGet("week-3", out var served));
        Assert.Same(second, served);
    }

    // Games (or schedule changes) that only a full-window fetch picks up must not wait out a whole
    // 10h+ Sunday/Saturday of back-to-back live windows.
    [Fact]
    public async Task RefreshAsync_StillDoesAFullFetch_EveryLiveFullRefreshInterval() {
        var clock = new FakeTimeProvider(Kickoff.AddMinutes(10));
        var snapshot = new PolledItemSnapshot(TimeSpan.FromMinutes(10), clock);
        var espn = new Espn();

        await snapshot.RefreshAsync("week-3", espn.FetchDays, espn.FetchAll);
        for (var t = TimeSpan.Zero; t < EspnPollCadence.LiveFullRefreshInterval; t += TimeSpan.FromMinutes(1)) {
            clock.Advance(TimeSpan.FromMinutes(1));
            await snapshot.RefreshAsync("week-3", espn.FetchDays, espn.FetchAll);
        }

        Assert.Equal(2, espn.Full);
    }

    [Fact]
    public async Task RefreshAsync_FullFetch_ForADifferentItem_OrWhenNothingIsLive() {
        var clock = new FakeTimeProvider(Kickoff.AddMinutes(10));
        var snapshot = new PolledItemSnapshot(TimeSpan.FromHours(24), clock); // isolate "nothing live" from maxAge
        var espn = new Espn();

        await snapshot.RefreshAsync("week-3", espn.FetchDays, espn.FetchAll);
        await snapshot.RefreshAsync("week-4", espn.FetchDays, espn.FetchAll); // new week
        Assert.Equal((2, 0), (espn.Full, espn.Day));

        clock.Advance(TimeSpan.FromHours(5)); // the game's live window has ended
        await snapshot.RefreshAsync("week-4", espn.FetchDays, espn.FetchAll);
        Assert.Equal((3, 0), (espn.Full, espn.Day));
    }
}
