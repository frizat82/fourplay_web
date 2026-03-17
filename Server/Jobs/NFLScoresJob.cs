using FourPlayWebApp.Server.Models.Data;
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
        var weekList = new List<NflWeeks>();
        for (var i = -2; i < 2; i++) {
            // Regular Season
            for (var j = 1; j < 19; j++) {
                // TODO: how do i know the year?
                var scores = await espn.GetWeekScores(j, DateTime.UtcNow.AddYears(i).Year);
                if (scores is null)
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
                // TODO: how do i know the year?
                var scores = await espn.GetWeekScores(j, DateTime.UtcNow.AddYears(i).Year, true);
                if (scores is null)
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

        if (scoreList.Count != 0) {
            await leagueRepository.UpsertNflScoresAsync(scoreList);
        }
        
        for (var i = -4; i < 4; i++) {
            var scores = await espn.GetSeasonScores(DateTime.UtcNow.AddYears(i).Year);
            if (scores.Leagues is null || scores.Leagues.Length == 0 || scores.Leagues[0].Calendar is null || scores.Leagues[0].Calendar.Length == 0)
                continue;
            var regularSeason = scores.Leagues[0].Calendar.FirstOrDefault(x => x.Value == (int)TypeOfSeason.RegularSeason);
            weekList.AddRange(regularSeason.Entries.Select(x => new NflWeeks()
            {
                NflWeek = (int)x.Value,
                Season = (int)scores.Leagues[0].Season.Year,
                StartDate = x.StartDate,
                EndDate = x.EndDate
            }));
            // Skip week 4 as ESPN treats week 4 as the Pro Bowl
            var postSeason = scores.Leagues[0].Calendar.FirstOrDefault(x => x.Value == (int)TypeOfSeason.PostSeason);
            weekList.AddRange(postSeason.Entries.Where(x => x.Value != 4).Select(x => new NflWeeks()
            {
                NflWeek = GameHelpers.GetWeekFromEspnWeek(x.Value == 5 ? 4 : x.Value, true),
                Season = (int)scores.Leagues[0].Season.Year,
                StartDate = x.StartDate,
                EndDate = x.EndDate
            }));
        }


        if (weekList.Count != 0) { 
            await leagueRepository.UpsertNflWeeksAsync(weekList);
        }

        Log.Information("Grabbed NFL scores at {Time}", DateTime.UtcNow);
    }
}