using System.Net;
using FourPlayWebApp.Server.Services;
using FourPlayWebApp.Server.UnitTests.TestHelpers;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;

namespace FourPlayWebApp.Server.UnitTests;

// frizat-11t: regular-season CFB fetch moved from ESPN's week=N bucket (which doesn't respect our
// slate's own date window — a team can appear twice in one week=N fetch, see CfbLiveScoreFetcherTests'
// USC/Fresno-vs-SJSU regression) to a dates=yyyyMMdd-yyyyMMdd range scoped to the control table's
// own WeekStartDate/WeekEndDate. This is a NEW query shape, not a revert to the old broken
// groups=80 date approach CfbApiService.GetScoresByWeekAsync's own comment references.
//
// frizat-4gn: ESPN only answers single-day dates= queries (a START-END range is HTTP 400), so
// GetScoresByDateRangeAsync fetches the window one day at a time (EspnDateRangeFetcher) — no
// range attempt first, which was one guaranteed-wasted ESPN request per window fetch.
public class CfbApiServiceTests {
    private static (CfbApiService sut, CapturingHandler handler) Build() {
        var handler = new CapturingHandler();
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://site.api.espn.com") };
        return (new CfbApiService(httpClient, NullLogger<CfbApiService>.Instance), handler);
    }

    [Fact]
    public async Task GetScoresByDateRangeAsync_RequestsEachDay_WithSeasonType2AndLimit100_AndNoRangeQuery() {
        var (sut, handler) = Build();

        await sut.GetScoresByDateRangeAsync(new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 3));

        Assert.Equal(["dates=20260901", "dates=20260902", "dates=20260903"],
            handler.RequestUris.Select(u => u.Query.Split('&').Single(p => p.TrimStart('?').StartsWith("dates=")).TrimStart('?')));
        Assert.All(handler.RequestUris, u => Assert.Contains("seasontype=2", u.Query));
        Assert.All(handler.RequestUris, u => Assert.Contains("limit=100", u.Query));
    }

    [Fact]
    public async Task GetScoresByDateRangeAsync_ParsesEventsFromResponse() {
        var (sut, handler) = Build();
        handler.ResponseBody = """
        {
          "season": { "type": 2, "year": 2026 },
          "week": { "number": 1 },
          "events": [
            {
              "id": "401858436",
              "season": { "type": 2, "year": 2026 },
              "week": { "number": 1 },
              "date": "2026-09-05T01:00Z",
              "competitions": [
                {
                  "id": "401858436",
                  "date": "2026-09-05T01:00Z",
                  "competitors": [
                    { "id": "1", "homeAway": "home", "score": "0", "team": { "abbreviation": "USC" } },
                    { "id": "2", "homeAway": "away", "score": "0", "team": { "abbreviation": "FRES" } }
                  ],
                  "status": {
                    "clock": 0, "displayClock": "0:00", "period": 0,
                    "type": { "id": "1", "name": "STATUS_SCHEDULED", "state": "pre", "completed": false, "description": "Scheduled" }
                  },
                  "odds": []
                }
              ]
            }
          ]
        }
        """;

        var result = await sut.GetScoresByDateRangeAsync(new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 7));

        Assert.NotNull(result);
        var evt = Assert.Single(result!.Events!);
        Assert.Equal("401858436", evt.Id);
    }

    [Fact]
    public async Task GetScoresByDateRangeAsync_NonSuccessStatus_ReturnsNull() {
        var (sut, handler) = Build();
        handler.StatusCode = HttpStatusCode.ServiceUnavailable;

        var result = await sut.GetScoresByDateRangeAsync(new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 7));

        Assert.Null(result);
    }

    // A day whose games are all final isn't requested again (EspnDayCache); a CFP bucket goes
    // through the same cache.
    [Fact]
    public async Task FinishedDays_AndTheCfpBucket_AreServedFromTheDayCache() {
        var handler = new CapturingHandler { ResponseBody = FinalDayBody };
        var sut = new CfbApiService(new HttpClient(handler) { BaseAddress = new Uri("http://site.api.espn.com") },
            NullLogger<CfbApiService>.Instance, new EspnDayCache(new MemoryCache(new MemoryCacheOptions()), TimeProvider.System));

        await sut.GetScoresByDateRangeAsync(new DateOnly(2026, 9, 12), new DateOnly(2026, 9, 13));
        await sut.GetScoresByDateRangeAsync(new DateOnly(2026, 9, 12), new DateOnly(2026, 9, 13));
        await sut.GetCfpGamesAsync();
        await sut.GetCfpGamesAsync();

        Assert.Equal(3, handler.RequestUris.Count); // 2 days + 1 CFP bucket, each fetched once
    }

    private const string FinalDayBody = """
    {
      "events": [
        {
          "id": "1", "season": { "type": 2, "year": 2026 }, "week": { "number": 3 },
          "date": "2026-09-12T17:00Z",
          "competitions": [
            {
              "id": "1", "date": "2026-09-12T17:00Z",
              "competitors": [
                { "id": "1", "homeAway": "home", "score": "21", "team": { "abbreviation": "USC" } },
                { "id": "2", "homeAway": "away", "score": "14", "team": { "abbreviation": "FRES" } }
              ],
              "status": {
                "clock": 0, "displayClock": "0:00", "period": 4,
                "type": { "id": "3", "name": "STATUS_FINAL", "state": "post", "completed": true, "description": "Final" }
              },
              "odds": []
            }
          ]
        }
      ]
    }
    """;
}
