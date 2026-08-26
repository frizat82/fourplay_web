using FourPlayWebApp.Server.Services.Interfaces;
using FourPlayWebApp.Server.Services.Repositories.Interfaces;
using FourPlayWebApp.Shared.Helpers;
using FourPlayWebApp.Shared.Helpers.Extensions;
using FourPlayWebApp.Shared.Models;
using FourPlayWebApp.Shared.Models.Data;
using FourPlayWebApp.Shared.Models.Enum;
using Quartz;
using Serilog;
namespace FourPlayWebApp.Server.Jobs;
[DisallowConcurrentExecution]
public class NflSpreadJob(IEspnCoreOddsService sportsOdds, IEspnApiService espn, ILeagueRepository leagueRepository, INflCurrentWeekService nflCurrentWeekService, TimeProvider timeProvider)
    : IJob {
    public async Task Execute(IJobExecutionContext context) {
        Log.Information("Grabbing NFL Spreads at {Time}",DateTime.UtcNow);
        var currentWeek = await nflCurrentWeekService.GetCurrentWeekAsync();

        if (SpreadLockGuard.ShouldSkip(currentWeek.SpreadLockDatetime, timeProvider.GetUtcNow().UtcDateTime, context)) {
            Log.Information("NflSpreadJob: skipping {Week} — lock time {LockTime} not yet reached", currentWeek.WeekLabel, currentWeek.SpreadLockDatetime);
            return;
        }

        var scoreboard = await espn.GetWeekScores(currentWeek.EspnWeek, currentWeek.Season, currentWeek.IsPostSeason);
        if (scoreboard is null)
            return;
        var isPostSeason = currentWeek.IsPostSeason;
        var newGames = scoreboard?.Events.SelectMany(x => x.Competitions, (x, y) => new CompetitionBySeason { Id = int.Parse(x.Id), Season = x.Season, Competition = y }).Where(y => y.Competition.Status.Type.Name == TypeName.StatusScheduled).ToList();
        if (newGames is null)
            return;
        if (newGames.Count == 0)
        {
            Log.Information("Bye week detected — no scheduled games found, skipping spread ingestion at {Time}", DateTime.UtcNow);
            return;
        }
        var week = currentWeek.WeekId;
        var spreads = new List<NflSpreads>();
        foreach (var games in newGames) {
            var spread = games.ParseCompetitionToNflSpreads(week);
            if (spread is null) {
                continue;
            }
            Log.Information("Grabbing NFL Spreads for {Game} {Time}",spread.HomeTeam, DateTime.UtcNow);
            try {
                var parsed = await SpreadOddsFetcher.FetchAsync(
                    sportsOdds.GetEventsWithOddsAsync, sportsOdds.GetEventsWithOddsAsync, games.Id, spread.HomeTeam);
                if (parsed is null) continue;
                spread.HomeTeamSpread = parsed.Value.HomeSpread;
                spread.AwayTeamSpread = parsed.Value.AwaySpread;
                spread.OverUnder = parsed.Value.OverUnder;
                spreads.Add(spread);
            }
            catch (Exception ex) {
                Log.Error(ex, "Unable to get spread for game {GameId}", games.Id);
            }
        }

        if (spreads.Count != 0) {
            Log.Information("Load NFL Spreads at {Time}", DateTime.UtcNow);
            await leagueRepository.UpsertAsync(spreads);
        }
        Log.Information("NFL Spreads Complete at {Time}",DateTime.UtcNow);
    }
}
