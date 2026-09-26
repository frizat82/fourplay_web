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
}
