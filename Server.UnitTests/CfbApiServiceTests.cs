using System.Net;
using FourPlayWebApp.Server.Services;
using FourPlayWebApp.Server.UnitTests.TestHelpers;
using Microsoft.Extensions.Logging.Abstractions;

namespace FourPlayWebApp.Server.UnitTests;

// frizat-11t: regular-season CFB fetch moved from ESPN's week=N bucket (which doesn't respect our
// slate's own date window — a team can appear twice in one week=N fetch, see CfbLiveScoreFetcherTests'
// USC/Fresno-vs-SJSU regression) to a dates=yyyyMMdd-yyyyMMdd range scoped to the control table's
// own WeekStartDate/WeekEndDate. This is a NEW query shape, not a revert to the old broken
// groups=80 date approach CfbApiService.GetScoresByWeekAsync's own comment references.
//
// frizat-4gn: ESPN broke the dates=START-END range query — GetScoresByDateRangeAsync now tries
// that range query first, and falls back to a day-by-day fetch (EspnDateRangeFetcher) only if it
// fails. The tests below still exercise the range-succeeds path by default (CapturingHandler
// defaults to 200 OK), so they're unaffected by the fallback machinery unless a test explicitly
// forces the range attempt to fail.
public class CfbApiServiceTests {
    private static (CfbApiService sut, CapturingHandler handler) Build() {
        var handler = new CapturingHandler();
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://site.api.espn.com") };
        return (new CfbApiService(httpClient, NullLogger<CfbApiService>.Instance), handler);
    }

    [Fact]
    public async Task GetScoresByDateRangeAsync_BuildsDatesUrl_WithSeasonType2AndLimit100() {
        var (sut, handler) = Build();

        await sut.GetScoresByDateRangeAsync(new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 7));

        Assert.NotNull(handler.LastRequestUri);
        var query = handler.LastRequestUri!.Query;
        Assert.Contains("dates=20260901-20260907", query);
        Assert.Contains("seasontype=2", query);
        Assert.Contains("limit=100", query);
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

    [Fact]
    public async Task GetScoresByDateRangeAsync_RangeQueryFails_FallsBackToDayByDay() {
        var (sut, handler) = Build();
        // The first call (the range attempt) fails; every call after that (day-by-day) succeeds.
        handler.ResponseQueue.Enqueue((HttpStatusCode.BadRequest, "{}"));
        handler.StatusCode = HttpStatusCode.OK;
        handler.ResponseBody = """
        {
          "events": [
            {
              "id": "1", "season": { "type": 2, "year": 2026 }, "week": { "number": 3 },
              "date": "2026-09-18T00:00Z",
              "competitions": [
                {
                  "id": "1", "date": "2026-09-18T00:00Z",
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

        var result = await sut.GetScoresByDateRangeAsync(new DateOnly(2026, 9, 15), new DateOnly(2026, 9, 17));

        Assert.NotNull(result);
        Assert.Single(result!.Events!); // same event id repeated by the stub every day — deduped
        // 1 range attempt + 3 single-day calls (Sep 15/16/17)
        Assert.Equal(4, handler.RequestUris.Count);
        Assert.Contains(handler.RequestUris, uri => uri.Query.Contains("dates=20260915-20260917"));
        Assert.Contains(handler.RequestUris, uri => uri.Query.Contains("dates=20260915") && !uri.Query.Contains("20260915-"));
    }
}
