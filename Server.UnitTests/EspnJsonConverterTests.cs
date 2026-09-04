using System.Net;
using System.Text;
using FourPlayWebApp.Server.Services;
using FourPlayWebApp.Shared.Models;

namespace FourPlayWebApp.Server.UnitTests;

/// <summary>
/// frizat-703.5 code-review follow-up: the live Contract tests proved CfbApiService.cs threw on
/// real "homeAway"/CFB record-type wire values, and the fix was consolidating everything onto
/// EspnApiServiceJsonConverter.Settings. But Contract tests are excluded from the default `dotnet
/// test` run (network-dependent, weekly only) — without an offline test, a future revert of
/// CfbApiService's `_opts` field back to a bare options object, or a revert of the EspnRecordType
/// enum, would pass every default PR check and go undetected for up to a week. These tests close
/// that gap: fast, offline, no network, run on every `dotnet test`.
/// </summary>
public class EspnJsonConverterTests
{
    [Theory]
    [InlineData("away", HomeAway.Away)]
    [InlineData("home", HomeAway.Home)]
    public void HomeAwayConverter_ParsesRealWireValues(string wireValue, HomeAway expected)
    {
        var json = $$"""{"homeAway":"{{wireValue}}"}""";
        var competitor = System.Text.Json.JsonSerializer.Deserialize<Competitor>(json, EspnApiServiceJsonConverter.Settings);

        Assert.Equal(expected, competitor!.HomeAway);
    }

    [Theory]
    [InlineData("road", EspnRecordType.Road)] // NFL
    [InlineData("home", EspnRecordType.Home)] // NFL
    [InlineData("total", EspnRecordType.Total)] // NFL + CFB
    [InlineData("homerecord", EspnRecordType.HomeRecord)] // CFB
    [InlineData("awayrecord", EspnRecordType.AwayRecord)] // CFB
    [InlineData("vsconf", EspnRecordType.VsConf)] // CFB
    public void EspnRecordTypeConverter_ParsesRealWireValues(string wireValue, EspnRecordType expected)
    {
        var json = $$"""{"type":"{{wireValue}}"}""";
        var record = System.Text.Json.JsonSerializer.Deserialize<EspnRecord>(json, EspnApiServiceJsonConverter.Settings);

        Assert.Equal(expected, record!.Type);
    }

    private class StubHttpMessageHandler(string responseJson) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(responseJson, Encoding.UTF8, "application/json"),
            });
    }

    // Minimal real-shaped CFB scoreboard payload: one event, one competition, two competitors
    // (homeAway "away"/"home"), one "homerecord" record. Regression guard for the exact defect
    // this PR fixed — CfbApiService throwing on real ESPN wire values.
    private const string MinimalCfbScoreboardJson = """
        {
          "events": [
            {
              "id": "1",
              "date": "2025-11-01T00:00Z",
              "competitions": [
                {
                  "id": "1",
                  "date": "2025-11-01T00:00Z",
                  "status": {
                    "clock": 0,
                    "displayClock": "0:00",
                    "period": 4,
                    "type": { "id": "3", "name": "STATUS_FINAL", "state": "post", "completed": true, "description": "Final", "detail": "Final", "shortDetail": "Final" }
                  },
                  "competitors": [
                    {
                      "id": "1",
                      "homeAway": "home",
                      "team": { "abbreviation": "IND" },
                      "score": "13",
                      "records": [ { "name": "overall", "type": "homerecord", "summary": "5-3" } ]
                    },
                    {
                      "id": "2",
                      "homeAway": "away",
                      "team": { "abbreviation": "ATL" },
                      "score": "14",
                      "records": [ { "name": "overall", "type": "awayrecord", "summary": "4-4" } ]
                    }
                  ]
                }
              ]
            }
          ]
        }
        """;

    [Fact]
    public async Task CfbApiService_GetScoresByDateRangeAsync_DeserializesRealWireValuesWithoutThrowing()
    {
        var httpClient = new HttpClient(new StubHttpMessageHandler(MinimalCfbScoreboardJson))
        {
            BaseAddress = new Uri("http://site.api.espn.com"),
        };
        var service = new CfbApiService(httpClient);

        var scores = await service.GetScoresByDateRangeAsync(new DateOnly(2025, 10, 25), new DateOnly(2025, 11, 1));

        var competitors = scores!.Events!.Single().Competitions.Single().Competitors;
        Assert.Equal(HomeAway.Home, competitors.Single(c => c.Team.Abbreviation == "IND").HomeAway);
        Assert.Equal(HomeAway.Away, competitors.Single(c => c.Team.Abbreviation == "ATL").HomeAway);
        Assert.Equal(EspnRecordType.HomeRecord, competitors.Single(c => c.Team.Abbreviation == "IND").Records.Single().Type);
    }

    // TypeNameConverter/DescriptionConverter previously threw on any status.type.name/description
    // value outside our known 5 — but ESPN's real wire values also include things like
    // STATUS_POSTPONED, STATUS_DELAYED, STATUS_CANCELED, STATUS_RAIN_DELAY, and STATUS_FORFEIT for
    // weather/scheduling edge cases. Because System.Text.Json aborts the ENTIRE deserialization on
    // any single property throwing, one game anywhere in a week's scoreboard entering one of these
    // states broke live status/scores for every OTHER game in that week too — PeriodicRefreshCache
    // (see PeriodicRefreshCache.cs) then silently keeps serving its last successfully-parsed
    // snapshot indefinitely, which is how a game already showed as stuck-"Final" days after actually
    // being scheduled: an unrelated game's odd status froze the whole week's cache at an earlier,
    // now-stale snapshot. Unknown/unusual statuses must fall back to Scheduled — "not decided yet"
    // is the only safe default; anything else risks showing a false Final (revealing a fake result,
    // wrongly locking picks) or a false Live/other state.
    [Theory]
    [InlineData("STATUS_POSTPONED")]
    [InlineData("STATUS_DELAYED")]
    [InlineData("STATUS_CANCELED")]
    [InlineData("STATUS_RAIN_DELAY")]
    [InlineData("STATUS_FORFEIT")]
    [InlineData("STATUS_SOME_FUTURE_ESPN_VALUE_WE_DONT_KNOW_ABOUT_YET")]
    public void TypeNameConverter_FallsBackToScheduled_ForUnrecognizedWireValues(string wireValue)
    {
        var json = $$"""{"id":"1","name":"{{wireValue}}","state":"pre","completed":false,"description":"Postponed","detail":"","shortDetail":""}""";

        var statusType = System.Text.Json.JsonSerializer.Deserialize<StatusType>(json, EspnApiServiceJsonConverter.Settings);

        Assert.Equal(TypeName.StatusScheduled, statusType!.Name);
    }

    [Theory]
    [InlineData("Postponed")]
    [InlineData("Delayed")]
    [InlineData("Canceled")]
    [InlineData("Some future ESPN description we don't know about yet")]
    public void DescriptionConverter_FallsBackToScheduled_ForUnrecognizedWireValues(string wireValue)
    {
        var json = $$"""{"id":"1","name":"STATUS_SCHEDULED","state":"pre","completed":false,"description":"{{wireValue}}","detail":"","shortDetail":""}""";

        var statusType = System.Text.Json.JsonSerializer.Deserialize<StatusType>(json, EspnApiServiceJsonConverter.Settings);

        Assert.Equal(Description.Scheduled, statusType!.Description);
    }

    // TypeName's first member (StatusFinal) is ordinal 0 — which is also C#'s default value for a
    // TypeName field that's never actually set. Our controllers re-serialize this enum as a raw
    // number (not our custom converter's string form — see EspnController), and the frontend
    // hardcodes STATUS_FINAL = 0 and checks isGameOver() BEFORE isGameStarted() (gameHelpers.ts's
    // toGameStatus). So a StatusType whose Name was never populated from JSON — e.g. ESPN's
    // response is missing the "name" key entirely, which the converter never even sees since
    // Read() is only invoked for a property that's present — would silently render as "Final" for
    // a game that hasn't even kicked off, with no exception anywhere. `required` makes System.Text.Json
    // track whether the property was actually set during deserialization, independent of the
    // custom converter, and throw JsonException if it wasn't — turning a silent wrong answer into
    // a loud, logged failure (which PeriodicRefreshCache/the direct fetch path already handle safely
    // by falling back to "scheduled" rather than showing a fabricated result).
    [Fact]
    public void StatusType_MissingNameProperty_ThrowsInsteadOfDefaultingToFinal()
    {
        const string json = """{"id":"1","state":"pre","completed":false,"description":"Scheduled","detail":"","shortDetail":""}""";

        Assert.Throws<System.Text.Json.JsonException>(() =>
            System.Text.Json.JsonSerializer.Deserialize<StatusType>(json, EspnApiServiceJsonConverter.Settings));
    }

    [Fact]
    public void StatusType_MissingDescriptionProperty_ThrowsInsteadOfDefaultingToFinal()
    {
        const string json = """{"id":"1","name":"STATUS_SCHEDULED","state":"pre","completed":false,"detail":"","shortDetail":""}""";

        Assert.Throws<System.Text.Json.JsonException>(() =>
            System.Text.Json.JsonSerializer.Deserialize<StatusType>(json, EspnApiServiceJsonConverter.Settings));
    }

    // The real-world failure mode: one game with an unrecognized status must not break every OTHER
    // game in the same scoreboard payload — this is what actually broke, not just the isolated
    // converter (System.Text.Json aborts the whole object graph on any single property throwing).
    [Fact]
    public async Task CfbApiService_GetScoresByDateRangeAsync_OneGameWithUnrecognizedStatus_DoesNotBreakOtherGames()
    {
        const string payload = """
            {
              "events": [
                {
                  "id": "1",
                  "date": "2025-11-01T00:00Z",
                  "competitions": [
                    {
                      "id": "1",
                      "date": "2025-11-01T00:00Z",
                      "status": {
                        "clock": 0, "displayClock": "0:00", "period": 0,
                        "type": { "id": "9", "name": "STATUS_POSTPONED", "state": "pre", "completed": false, "description": "Postponed", "detail": "Postponed", "shortDetail": "Postponed" }
                      },
                      "competitors": [
                        { "id": "1", "homeAway": "home", "team": { "abbreviation": "USC" }, "score": "0", "records": [] },
                        { "id": "2", "homeAway": "away", "team": { "abbreviation": "FRES" }, "score": "0", "records": [] }
                      ]
                    }
                  ]
                },
                {
                  "id": "2",
                  "date": "2025-11-01T00:00Z",
                  "competitions": [
                    {
                      "id": "2",
                      "date": "2025-11-01T00:00Z",
                      "status": {
                        "clock": 0, "displayClock": "0:00", "period": 2,
                        "type": { "id": "2", "name": "STATUS_IN_PROGRESS", "state": "in", "completed": false, "description": "In Progress", "detail": "In Progress", "shortDetail": "In Progress" }
                      },
                      "competitors": [
                        { "id": "3", "homeAway": "home", "team": { "abbreviation": "OU" }, "score": "14", "records": [] },
                        { "id": "4", "homeAway": "away", "team": { "abbreviation": "UTEP" }, "score": "7", "records": [] }
                      ]
                    }
                  ]
                }
              ]
            }
            """;
        var httpClient = new HttpClient(new StubHttpMessageHandler(payload)) { BaseAddress = new Uri("http://site.api.espn.com") };
        var service = new CfbApiService(httpClient);

        var scores = await service.GetScoresByDateRangeAsync(new DateOnly(2025, 9, 1), new DateOnly(2025, 9, 7));

        Assert.Equal(2, scores!.Events!.Length);
        var uscComp = scores.Events.Single(e => e.Competitions[0].Competitors.Any(c => c.Team.Abbreviation == "USC")).Competitions[0];
        Assert.Equal(TypeName.StatusScheduled, uscComp.Status.Type.Name); // postponed falls back to scheduled, not Final
        var utepComp = scores.Events.Single(e => e.Competitions[0].Competitors.Any(c => c.Team.Abbreviation == "UTEP")).Competitions[0];
        Assert.Equal(TypeName.StatusInProgress, utepComp.Status.Type.Name); // the other game parses normally, unaffected
    }
}
