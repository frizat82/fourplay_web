using FourPlayWebApp.Server.Services.Interfaces;
using FourPlayWebApp.Server.Services.Repositories.Interfaces;
using FourPlayWebApp.Shared.Models;
using FourPlayWebApp.Shared.Models.Data;
using FourPlayWebApp.Shared.Models.Enum;
using Quartz;
using Serilog;

namespace FourPlayWebApp.Server.Jobs;

[DisallowConcurrentExecution]
public class CfbScoresJob(ICfbApiService cfbApi, ICfbRepository repo) : IJob {
    private const int Season = 2026;

    public async Task Execute(IJobExecutionContext context) {
        Log.Information("CfbScoresJob: fetching CFB scores at {Time}", DateTime.UtcNow);

        var slates = (await repo.GetSlatesForSeasonAsync(Season)).ToList();
        if (slates.Count == 0) {
            Log.Warning("CfbScoresJob: no slates found for season {Season}", Season);
            return;
        }

        var scores = new List<CfbScores>();

        foreach (var slate in slates) {
            if (slate.EspnWeekNumber.HasValue) {
                if (CfbSlateHelpers.IsCfpSlate(slate.ScoringFormat)) {
                    // CFP: week=999 ESPN bucket returns all CFP games; filter to this round by date
                    var scoreboard = await cfbApi.GetCfpGamesAsync();
                    if (scoreboard?.Events is null) continue;
                    var cfpEvents = scoreboard.Events.Where(e => {
                        var comp = e.Competitions.FirstOrDefault();
                        return comp is not null
                            && comp.Date.Date >= slate.StartDate.ToDateTime(TimeOnly.MinValue).Date
                            && comp.Date.Date <= slate.EndDate.ToDateTime(TimeOnly.MaxValue).Date;
                    });
                    AppendScores(scores, slate, cfpEvents);
                } else {
                    // Regular season / conf-champs: filter to ranked teams only
                    var scoreboard = await cfbApi.GetScoresByWeekAsync(slate.EspnWeekNumber.Value, isPostSeason: false);
                    if (scoreboard?.Events is null) continue;
                    var rankedEvents = scoreboard.Events.Where(e =>
                        e.Competitions.Any(c => CfbSlateHelpers.HasRankedTeam(c.Competitors)));
                    AppendScores(scores, slate, rankedEvents);
                }
            } else {
                // Legacy fallback: date-range iteration for slates missing EspnWeekNumber
                for (var date = slate.StartDate; date <= slate.EndDate; date = date.AddDays(1)) {
                    var daily = CfbSlateHelpers.IsTop25Slate(slate.SlateType)
                        ? await cfbApi.GetTop25ByDateAsync(date)
                        : await cfbApi.GetScoresByDateAsync(date);
                    if (daily?.Events is null) continue;
                    AppendScores(scores, slate, daily.Events);
                }
            }
        }

        if (scores.Count > 0) {
            await repo.UpsertCfbScoresAsync(scores);
            Log.Information("CfbScoresJob: upserted {Count} CFB scores", scores.Count);
        }
        Log.Information("CfbScoresJob: complete at {Time}", DateTime.UtcNow);
    }

    private static void AppendScores(List<CfbScores> scores, FourPlayWebApp.Server.Models.Data.CfbSlates slate, IEnumerable<FourPlayWebApp.Shared.Models.Event> events) {
        foreach (var evt in events) {
            var comp = evt.Competitions.FirstOrDefault();
            if (comp is null) continue;

            var status = comp.Status.Type.Name;
            if (status != TypeName.StatusFinal && status != TypeName.StatusInProgress) continue;

            var home = comp.Competitors.FirstOrDefault(c => c.HomeAway == HomeAway.Home);
            var away = comp.Competitors.FirstOrDefault(c => c.HomeAway == HomeAway.Away);
            if (home is null || away is null) continue;

            scores.Add(new CfbScores {
                CfbSlateId          = slate.Id,
                EspnEventId         = int.Parse(evt.Id),
                HomeTeam            = home.Team.Abbreviation,
                AwayTeam            = away.Team.Abbreviation,
                HomeTeamScore       = (int)home.Score,
                AwayTeamScore       = (int)away.Score,
                GameStatus          = status.ToString(),
                GameTime            = comp.Date,
                WeatherDisplayValue = evt.Weather?.DisplayValue,
                WeatherConditionId  = evt.Weather?.ConditionId,
                WeatherTemperatureF = evt.Weather?.Temperature,
            });
        }
    }
}
