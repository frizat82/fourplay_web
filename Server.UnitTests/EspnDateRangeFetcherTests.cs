using FourPlayWebApp.Server.Services;
using FourPlayWebApp.Shared.Models;

namespace FourPlayWebApp.Server.UnitTests;

// frizat-4gn: ESPN's dates=START-END range query started returning HTTP 400 for every range
// (both NFL and CFB scoreboard endpoints), while a single date still works — this is the shared
// day-by-day-fetch-and-merge replacement both EspnApiService (NFL) and CfbApiService (CFB) call
// into. Tested here against the pure merge logic, independent of HTTP, since that's the genuinely
// new/risky part of this fix — each service's own tests cover the URL/wiring side.
public class EspnDateRangeFetcherTests {
    private static Event MakeEvent(string id) => new() { Id = id, Competitions = [] };

    private static EspnScores MakeScores(params Event[] events) => new() { Events = events };

    [Fact]
    public async Task FetchRangeAsync_MergesEventsAcrossMultipleDays() {
        var byDate = new Dictionary<DateOnly, EspnScores?> {
            [new DateOnly(2026, 9, 15)] = MakeScores(MakeEvent("1")),
            [new DateOnly(2026, 9, 16)] = MakeScores(MakeEvent("2")),
            [new DateOnly(2026, 9, 17)] = MakeScores(MakeEvent("3")),
        };

        var result = await EspnDateRangeFetcher.FetchRangeAsync(
            new DateOnly(2026, 9, 15), new DateOnly(2026, 9, 17), date => Task.FromResult(byDate[date]));

        Assert.NotNull(result);
        Assert.Equal(["1", "2", "3"], result!.Events!.Select(e => e.Id));
    }

    [Fact]
    public async Task FetchRangeAsync_DeduplicatesTheSameEventIdAcrossDays() {
        // A late-kickoff game can land in two adjacent single-day ESPN buckets depending on
        // timezone rounding — the same event must not appear twice in the merged result.
        var byDate = new Dictionary<DateOnly, EspnScores?> {
            [new DateOnly(2026, 9, 15)] = MakeScores(MakeEvent("1")),
            [new DateOnly(2026, 9, 16)] = MakeScores(MakeEvent("1"), MakeEvent("2")),
        };

        var result = await EspnDateRangeFetcher.FetchRangeAsync(
            new DateOnly(2026, 9, 15), new DateOnly(2026, 9, 16), date => Task.FromResult(byDate[date]));

        Assert.NotNull(result);
        Assert.Equal(["1", "2"], result!.Events!.Select(e => e.Id));
    }

    [Fact]
    public async Task FetchRangeAsync_EveryDayFails_ReturnsNull() {
        var result = await EspnDateRangeFetcher.FetchRangeAsync(
            new DateOnly(2026, 9, 15), new DateOnly(2026, 9, 17), _ => Task.FromResult<EspnScores?>(null));

        Assert.Null(result);
    }

    [Fact]
    public async Task FetchRangeAsync_OneDayFailsAmongOthers_ReturnsTheSucceedingDaysEvents() {
        var byDate = new Dictionary<DateOnly, EspnScores?> {
            [new DateOnly(2026, 9, 15)] = MakeScores(MakeEvent("1")),
            [new DateOnly(2026, 9, 16)] = null,
            [new DateOnly(2026, 9, 17)] = MakeScores(MakeEvent("3")),
        };

        var result = await EspnDateRangeFetcher.FetchRangeAsync(
            new DateOnly(2026, 9, 15), new DateOnly(2026, 9, 17), date => Task.FromResult(byDate[date]));

        Assert.NotNull(result);
        Assert.Equal(["1", "3"], result!.Events!.Select(e => e.Id));
    }

    [Fact]
    public async Task FetchRangeAsync_SingleDayRange_MakesExactlyOneCall() {
        var callCount = 0;
        await EspnDateRangeFetcher.FetchRangeAsync(
            new DateOnly(2026, 9, 15), new DateOnly(2026, 9, 15),
            _ => { callCount++; return Task.FromResult<EspnScores?>(MakeScores(MakeEvent("1"))); });

        Assert.Equal(1, callCount);
    }
}
