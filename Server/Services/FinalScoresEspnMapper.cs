using FourPlayWebApp.Shared.Models;

namespace FourPlayWebApp.Server.Services;

// Builds an EspnScores wire-shape response from this app's own persisted, already-FINAL score
// rows (NflScores/CfbScores) — one shared implementation both sports' "serve a historical
// week/slate from the DB instead of a live ESPN call" paths call, since DB only ever persists a
// game once it's FINAL (NflScoresJob, CfbScoresJob). Extracted from what was previously a
// private, NFL-only helper in DemoEspnCacheService.BuildEvent.
public static class FinalScoresEspnMapper {
    public readonly record struct WeatherInfo(string? DisplayValue, string? ConditionId, int? TemperatureF);

    public readonly record struct FinishedGame(
        string Id, string HomeTeam, string AwayTeam,
        int HomeScore, int AwayScore, DateTimeOffset GameTime,
        WeatherInfo? Weather = null);

    public static EspnScores Build(IEnumerable<FinishedGame> games, int season, int week, bool postSeason) {
        var events = games.Select(g => BuildEvent(g, season, week, postSeason)).ToArray();
        return new EspnScores {
            Leagues = [],
            Season = new Season { Year = season, Type = postSeason ? 3 : 2 },
            Week = new Week { Number = week },
            Events = events,
        };
    }

    private static Event BuildEvent(FinishedGame game, int season, int week, bool postSeason) {
        var competition = new Competition {
            Id = game.Id,
            Date = game.GameTime,
            Competitors = [
                new Competitor { Id = "1", HomeAway = HomeAway.Home, Team = new EspnTeam { Abbreviation = game.HomeTeam }, Score = game.HomeScore, Records = [] },
                new Competitor { Id = "2", HomeAway = HomeAway.Away, Team = new EspnTeam { Abbreviation = game.AwayTeam }, Score = game.AwayScore, Records = [] },
            ],
            Status = new EspnStatus {
                Clock = 0,
                DisplayClock = "0:00",
                Period = 4,
                Type = new StatusType {
                    Id = 3,
                    Name = TypeName.StatusFinal,
                    State = State.Post,
                    Completed = true,
                    Description = Description.Final,
                    Detail = "Final",
                    ShortDetail = "Final",
                },
            },
            Odds = [],
            Situation = null,
        };

        return new Event {
            Id = game.Id,
            Season = new Season { Year = season, Type = postSeason ? 3 : 2 },
            Week = new Week { Number = week },
            Date = game.GameTime,
            Competitions = [competition],
            Weather = game.Weather is { } w ? new EspnWeather {
                DisplayValue = w.DisplayValue,
                ConditionId = w.ConditionId,
                Temperature = w.TemperatureF ?? 0,
                HighTemperature = w.TemperatureF ?? 0,
            } : null,
        };
    }
}
