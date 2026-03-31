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
public class NflSpreadJob(IEspnCoreOddsService sportsOdds, IEspnApiService espn, ILeagueRepository leagueRepository)
    : IJob {
    public async Task Execute(IJobExecutionContext context) {
        Log.Information("Grabbing NFL Spreads at {Time}",DateTime.UtcNow);
        var scoreboard = await espn.GetScores();
        if (scoreboard is null)
            return;
        var isPostSeason = scoreboard.IsPostSeason();
        var newGames = scoreboard?.Events.SelectMany(x => x.Competitions, (x, y) => new CompetitionBySeason { Id = int.Parse(x.Id), Season = x.Season, Competition = y }).Where(y => y.Competition.Status.Type.Name == TypeName.StatusScheduled).ToList();
        if (newGames is null)
            return;
        if (newGames.Count == 0)
        {
            Log.Information("Bye week detected — no scheduled games found, skipping spread ingestion at {Time}", DateTime.UtcNow);
            return;
        }
        var week = GameHelpers.GetWeekFromEspnWeek(scoreboard.Week.Number, isPostSeason);
        var spreads = new List<NflSpreads>();
        foreach (var games in newGames) {
            var spread = games.ParseCompetitionToNflSpreads(week);
            if (spread is null) {
                continue;
            }
            Log.Information("Grabbing NFL Spreads for {Game} {Time}",spread.HomeTeam, DateTime.UtcNow);
            try {
                var result = await sportsOdds.GetEventsWithOddsAsync(games.Id, (int)EspnOddsProviders.DraftKings);
                if (result is null) {
                    Log.Error("Spread not available using DraftKings for {Game}, trying default Spreads", spread.HomeTeam);
                    var allResults = await sportsOdds.GetEventsWithOddsAsync(games.Id);
                    if (allResults is null || allResults.Count == 0) {
                        Log.Error("No spreads available for {Game}, moving on", spread.HomeTeam);
                        continue;
                    }
                    Log.Warning("Not using ESPNBet, found spread from {Provider} {Game}", allResults.Items.First().Provider.Name, spread.HomeTeam);
                    result = allResults.Items.First();
                }
                var cleanHomeSpread = result.HomeTeamOdds.Current.PointSpread.American.Replace("+", "");
                var cleanAwaySpread = result.AwayTeamOdds.Current.PointSpread.American.Replace("+", "");
                if (cleanHomeSpread == "FK") {
                    Log.Error("Error");
                }

                if (cleanAwaySpread == "FK") {
                    Log.Error("Error");
                }

                if (!double.TryParse(cleanHomeSpread, out var parsedSpread))
                    continue;
                spread.HomeTeamSpread = parsedSpread;
                if (!double.TryParse(cleanAwaySpread, out parsedSpread))
                    continue;
                spread.AwayTeamSpread = parsedSpread;
                spread.OverUnder = result.OverUnder;
                spreads.Add(spread);
            }
            catch (Exception ex) {
                Log.Error(ex, "Unable to get spread for game {GameId}", games.Id);
            }
        }

        if (spreads.Count != 0) {
            Log.Information("Load NFL Spreads at {Time}", DateTime.UtcNow);
            await leagueRepository.AddNewNflSpreadsAsync(spreads);
        }
        Log.Information("NFL Spreads Complete at {Time}",DateTime.UtcNow);
    }
}
