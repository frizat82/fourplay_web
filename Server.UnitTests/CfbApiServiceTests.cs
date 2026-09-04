using System.Net;
using FourPlayWebApp.Server.Services;

namespace FourPlayWebApp.Server.UnitTests;

// frizat-11t: regular-season CFB fetch moved from ESPN's week=N bucket (which doesn't respect our
// slate's own date window — a team can appear twice in one week=N fetch, see CfbLiveScoreFetcherTests'
// USC/Fresno-vs-SJSU regression) to a dates=yyyyMMdd-yyyyMMdd range scoped to the control table's
// own WeekStartDate/WeekEndDate. This is a NEW query shape, not a revert to the old broken
// groups=80 date approach CfbApiService.GetScoresByWeekAsync's own comment references.
public class CfbApiServiceTests {
    private sealed class CapturingHandler : HttpMessageHandler {
        public Uri? LastRequestUri { get; private set; }
        public string ResponseBody { get; set; } = "{}";
        public HttpStatusCode StatusCode { get; set; } = HttpStatusCode.OK;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) {
            LastRequestUri = request.RequestUri;
            return Task.FromResult(new HttpResponseMessage(StatusCode) {
                Content = new StringContent(ResponseBody),
            });
        }
    }

    private static (CfbApiService sut, CapturingHandler handler) Build() {
        var handler = new CapturingHandler();
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://site.api.espn.com") };
        return (new CfbApiService(httpClient), handler);
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
}
