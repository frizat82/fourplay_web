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
public class NflSpreadJob(
    IEspnCoreOddsService sportsOdds,
    INflLiveScoreFetcher fetcher,
    ILeagueRepository leagueRepository,
    INflCurrentWeekService nflCurrentWeekService,
    TimeProvider timeProvider,
    IJobObserverService observer)
    : IJob {
    public async Task Execute(IJobExecutionContext context) {
        var jobName = context.JobDetail.Key.Name;
        try {
            Log.Information("Grabbing NFL Spreads at {Time}", DateTime.UtcNow);
            var currentWeek = await nflCurrentWeekService.GetCurrentWeekAsync();

            if (SpreadLockGuard.ShouldSkip(currentWeek.SpreadLockDatetime, timeProvider.GetUtcNow().UtcDateTime, context)) {
                Log.Information("NflSpreadJob: skipping {Week} — lock time {LockTime} not yet reached", currentWeek.WeekLabel, currentWeek.SpreadLockDatetime);
                await observer.RecordJobSuccessAsync(jobName, $"Skipped {currentWeek.WeekLabel} — lock time {currentWeek.SpreadLockDatetime} not yet reached");
                return;
            }

            // Mirrors CfbSpreadJob exactly: resolve the full control-table row for the current
            // week (NflWeekInfo alone carries no date window) and fetch by date range — never
            // trust ESPN's own week=N bucketing (frizat-11t's NFL mirror).
            var configs = await leagueRepository.GetNflSeasonWeekConfigsAsync(currentWeek.Season);
            var config = configs.FirstOrDefault(c => c.WeekId == currentWeek.WeekId);
            if (config is null) {
                Log.Warning("NflSpreadJob: no NflSeasonWeekConfig row for {Season} week {WeekId}", currentWeek.Season, currentWeek.WeekId);
                await observer.RecordJobSuccessAsync(jobName, $"No NflSeasonWeekConfig row for {currentWeek.Season} week {currentWeek.WeekId}");
                return;
            }

            var scoreboard = await fetcher.FetchForWeekAsync(config);
            if (scoreboard is null) {
                throw new InvalidOperationException(
                    $"NflSpreadJob: ESPN fetch failed entirely for {currentWeek.WeekLabel} (past lock time {currentWeek.SpreadLockDatetime}) — no scoreboard data retrieved");
            }

            // frizat-4gn: individual teams have byes, but the league as a whole never has zero
            // scheduled games in an in-scope week — newGames.Count == 0 here used to be logged
            // as an expected "bye week" and quietly skipped, but that's not actually a real state
            // for the league-wide schedule; it's the same silent-failure shape as a totally
            // failed fetch, just one step further along. No early return: this flows into the
            // same "zero spreads past lock time" alert check below.
            var newGames = scoreboard.Events.SelectMany(x => x.Competitions, (x, y) => new CompetitionBySeason { Id = int.Parse(x.Id), Season = x.Season, Competition = y }).Where(y => y.Competition.Status.Type.Name == TypeName.StatusScheduled).ToList();
            var week = currentWeek.WeekId;
            var spreads = new List<NflSpreads>();
            foreach (var games in newGames) {
                var spread = games.ParseCompetitionToNflSpreads(week);
                if (spread is null) {
                    continue;
                }
                Log.Information("Grabbing NFL Spreads for {Game} {Time}", spread.HomeTeam, DateTime.UtcNow);
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

            // frizat-4gn (NFL mirror): the schedule exists specifically so this job runs once
            // real games' odds should already be posted for the league's full slate — there's no
            // legitimate reason for spreads.Count to end up 0 here, whether that's because
            // newGames itself came back empty (see comment above) or every game's own odds
            // fetch failed. Throwing lets the existing JobFailureAlertListener/
            // DiscordJobFailureNotifier pipeline actually fire, same as every other job already
            // relies on.
            if (spreads.Count == 0) {
                throw new InvalidOperationException(
                    $"NflSpreadJob: {currentWeek.WeekLabel} has {newGames.Count} scheduled game(s) past lock time {currentWeek.SpreadLockDatetime}, but zero spreads were saved");
            }

            Log.Information("Load NFL Spreads at {Time}", DateTime.UtcNow);
            await leagueRepository.UpsertAsync(spreads);
            Log.Information("NFL Spreads Complete at {Time}", DateTime.UtcNow);
            await observer.RecordJobSuccessAsync(jobName, $"Saved {spreads.Count} spreads for {currentWeek.WeekLabel}");
        } catch (Exception ex) {
            await observer.RecordAndRethrowAsync(jobName, ex);
        }
    }
}
