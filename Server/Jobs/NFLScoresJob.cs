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

// frizat: rewritten to be control-table-driven, mirroring CfbScoresJob's
// GetSlatesForSeasonAsync(Season) loop exactly (CLAUDE.md: NFL/CFB are siblings, share the same
// mechanism) — no more hardcoded "-2..+1 years, weeks 1-18" sweep that trusted ESPN to fill in
// the gaps. Also stops trusting ESPN's own week=N bucketing at all (frizat-11t's NFL mirror):
// every fetch is scoped to the control table's own date window via INflLiveScoreFetcher.
[DisallowConcurrentExecution]
public class NflScoresJob(
    INflLiveScoreFetcher fetcher,
    ILeagueRepository leagueRepository,
    INflCurrentWeekService currentWeekService,
    IEspnCacheService espnCacheService) : IJob {
    public async Task Execute(IJobExecutionContext context) {
        Log.Information("Grabbing NFL scores at {Time}", DateTime.UtcNow);

        // Seed NflWeeks from NflSeasonWeekConfig (our control table) instead of ESPN calendar —
        // every season on record, not just the currently active one; a cheap DB-only sync, no
        // ESPN call, so the off-season gate below doesn't need to cover it.
        var allConfigs = await leagueRepository.GetNflSeasonWeekConfigsAsync();
        var weekList = allConfigs.Select(c => new NflWeeks {
            NflWeek = c.WeekId,
            Season = c.Season,
            StartDate = c.WeekStartDatetime,
            EndDate = c.WeekEndDatetime,
        }).ToList();

        // Beyond "configs exist" — is a season actually happening right now? Without this, this
        // loop would hit ESPN for every configured week every scheduled run, in-season or not.
        if (!await currentWeekService.IsSeasonActiveAsync()) {
            Log.Information("NflScoresJob: no season currently active, skipping ESPN fetch");
        } else {
            var currentWeek = await currentWeekService.GetCurrentWeekAsync();
            // allConfigs (above) already holds every season on record — filter in memory instead
            // of a second DB round trip for what's already in hand.
            var configs = allConfigs.Where(c => c.Season == currentWeek.Season).ToList();

            var scoreList = new List<NflScores>();
            // /code-review: a config's own window is compared to a game's kickoff by calendar
            // date, not exact instant (see GameHelpers.FilterEventsToDateWindow) — a boundary game
            // (e.g. Monday Night Football finishing in the small hours UTC) can fall on the same
            // calendar day as BOTH the ending week's cutoff and the next week's start, and so
            // legitimately match both configs' windows. configs is WeekId-ascending, so dedupe by
            // the game's own identity and keep the first (earliest, correct) week it was found
            // under — otherwise UpsertNflScoresAsync's (Season, NflWeek, HomeTeam) key doesn't
            // catch this at all, since NflWeek genuinely differs between the two matches.
            var seenGames = new HashSet<(int Season, string HomeTeam, string AwayTeam, DateTimeOffset GameTime)>();
            foreach (var config in configs) {
                var scoreboard = await fetcher.FetchForWeekAsync(config);
                if (scoreboard?.Events is null) continue;

                var results = scoreboard.Events.SelectMany(x => x.Competitions,
                        (x, y) => new CompetitionBySeason { Id = int.Parse(x.Id), Season = x.Season, Competition = y })
                    .Where(y => GameHelpers.IsGameOver(y.Competition)).ToList();
                foreach (var score in results.ParseCompetitionToNflScore(config.WeekId)) {
                    if (seenGames.Add((score.Season, score.HomeTeam, score.AwayTeam, score.GameTime))) {
                        scoreList.Add(score);
                    }
                }
            }

            if (scoreList.Count != 0) {
                Log.Information("Load NFL Scores at {Time}", DateTime.UtcNow);
                await leagueRepository.UpsertNflScoresAsync(scoreList);

                // The viewer-facing cache (EspnCacheService) can already hold a stale
                // reconstruction for a week that "ended" before this run discovered new/updated
                // finals for it — without this, a fresh upsert wouldn't be visible on the Scores
                // page until process restart. Mirrors CfbScoresJob's identical invalidation.
                foreach (var (season, week) in scoreList.Select(s => (s.Season, s.NflWeek)).Distinct()) {
                    espnCacheService.InvalidateWeekCache(season, week);
                }
            }
        }

        if (weekList.Count != 0) {
            await leagueRepository.UpsertNflWeeksAsync(weekList);
        }

        Log.Information("Grabbed NFL scores at {Time}", DateTime.UtcNow);
    }
}
