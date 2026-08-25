using FourPlayWebApp.Server.Models.Data;
using FourPlayWebApp.Server.Services;
using FourPlayWebApp.Server.Services.Interfaces;
using FourPlayWebApp.Server.Services.Repositories.Interfaces;
using FourPlayWebApp.Shared.Helpers;
using FourPlayWebApp.Shared.Helpers.Extensions;
using FourPlayWebApp.Shared.Models;
using FourPlayWebApp.Shared.Models.Data;
using Quartz;
using Serilog;

namespace FourPlayWebApp.Server.Jobs;

[DisallowConcurrentExecution]
public class NflScoresJob(IEspnApiService espn, ILeagueRepository leagueRepository) : IJob {
    public async Task Execute(IJobExecutionContext context) {
        Log.Information("Grabbing NFL scores at {Time}", DateTime.UtcNow);
        var scoreList = new List<NflScores>();

        // Seed NflWeeks from NflSeasonWeekConfig (our control table) instead of ESPN calendar
        var configs = await leagueRepository.GetNflSeasonWeekConfigsAsync();
        var weekList = configs.Select(c => new NflWeeks {
            NflWeek = c.WeekId,
            Season = c.Season,
            StartDate = c.WeekStartDatetime,
            EndDate = c.WeekEndDatetime,
        }).ToList();

        // Beyond "configs exist" — is a season actually happening right now? Without this, this
        // loop hits ESPN for up to 5 years x 22 weeks every scheduled run, in-season or not.
        var windows = configs.Select(c => new SeasonWindowResolver.Window(c.Season, c.WeekStartDatetime, c.WeekEndDatetime));
        if (!SeasonWindowResolver.IsSeasonActive(windows, DateTime.UtcNow)) {
            Log.Information("NflScoresJob: no season currently active, skipping ESPN fetch");
        } else {
            for (var i = -2; i < 2; i++) {
                // Regular Season
                for (var j = 1; j < 19; j++) {
                    var scores = await espn.GetWeekScores(j, DateTime.UtcNow.AddYears(i).Year);
                    if (scores is null || scores.Events is null)
                        break;
                    var results = scores.Events.SelectMany(x => x.Competitions,
                            (x, y) => new CompetitionBySeason { Id = int.Parse(x.Id), Season = x.Season, Competition = y })
                        .Where(y => GameHelpers.IsGameOver(y.Competition)).ToList();
                    if (results.Count != 0) {
                        scoreList.AddRange(results.ParseCompetitionToNflScore(GameHelpers.GetWeekFromEspnWeek(j)));
                    }
                }

                for (var j = 1; j < 6; j++) {
                    if (j == 4)
                        continue; // Skip week 4 as ESPN treats week 4 as the Pro Bowl
                    var scores = await espn.GetWeekScores(j, DateTime.UtcNow.AddYears(i).Year, true);
                    if (scores is null || scores.Events is null)
                        break;
                    var results = scores.Events.SelectMany(x => x.Competitions,
                            (x, y) => new CompetitionBySeason { Id = int.Parse(x.Id), Season = x.Season, Competition = y })
                        .Where(y => GameHelpers.IsGameOver(y.Competition)).ToList();
                    if (results.Count != 0) {
                        scoreList.AddRange(
                            results.ParseCompetitionToNflScore(GameHelpers.GetWeekFromEspnWeek(j == 5 ? 4 : j, true)));
                    }
                }
            }
        }

        if (scoreList.Count != 0) {
            await leagueRepository.UpsertNflScoresAsync(scoreList);
        }

        if (weekList.Count != 0) {
            await leagueRepository.UpsertNflWeeksAsync(weekList);
        }

        Log.Information("Grabbed NFL scores at {Time}", DateTime.UtcNow);
    }
}
