using FourPlayWebApp.Server.Jobs;
using FourPlayWebApp.Server.Models.Data;
using FourPlayWebApp.Server.Services.Interfaces;
using FourPlayWebApp.Server.Services.Repositories.Interfaces;
using FourPlayWebApp.Shared.Models;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Serilog;

namespace FourPlayWebApp.Server.Services;

// ICfbRepository is registered Scoped, but this fetcher is a Singleton — resolving it directly in
// the constructor would be a captive-dependency DI violation (same pattern CfbCacheService already
// uses for the identical problem; see its own comment).
public class CfbLiveScoreFetcher(ICfbApiService cfbApi, IServiceScopeFactory scopeFactory, IMemoryCache settledCache) : ICfbLiveScoreFetcher {
    public async Task<EspnScores?> FetchForSlateAsync(CfbSlates slate) {
        // CfbSeasonWeekConfig.EspnWeekNumber is non-nullable, and both known producers of CfbSlates
        // rows (CfbSlateSeederJob, DemoDataSeeder) always copy a real week number through — so every
        // slate in practice carries one, and this app only ever makes week-based ESPN queries, never
        // date-range "scoreboard" calls. CfbSlates.EspnWeekNumber itself is still nullable at the
        // model/DB level though, so a future producer could leave it unset — a missing value here
        // means the control table wasn't seeded correctly, not a case to silently paper over with a
        // date-range fallback. Checked up front so it applies uniformly to the DB-first branch below
        // too — /code-review caught that branch silently defaulting a missing value to week 0
        // instead of applying this same guard.
        if (!slate.EspnWeekNumber.HasValue) {
            Log.Warning("CfbLiveScoreFetcher: slate {SlateId} has no EspnWeekNumber — skipping", slate.Id);
            return null;
        }

        // DB-first: only once the slate's own window has fully ended — while a slate is still
        // active, ESPN is always the source of truth regardless of how many of its games have
        // already been persisted. /code-review caught a real bug in an earlier version of this
        // check that gated purely on "any row persisted": the instant ONE game in a multi-game
        // slate finished, every subsequent call permanently stopped calling ESPN, so the rest of
        // that slate's still-in-progress games were never discovered or persisted — this backs
        // both CfbScoresJob (which would never finish collecting that slate) and CfbCacheService's
        // live poll of the CURRENT slate (which would freeze the Scores page mid-slate). Once the
        // window has ended, a slate whose games are all persisted is always FINAL (CfbScoresJob
        // only writes finished games), so 100 concurrent viewers of the same past slate share one
        // DB read instead of each triggering its own ESPN call — mirrors EspnCacheService's
        // identical fix for NFL's historical-week endpoint.
        //
        // EndDate is a DateOnly with no time component, but a late game (common for CFB evening
        // kickoffs) can still be in progress after UTC midnight on that calendar date — a straight
        // date compare would flip to "ended" mid-game. The 6-hour buffer past the end of EndDate
        // covers that crossover; this now gets cached forever once true (see the caching layer
        // below), so getting this boundary right matters more than it used to.
        //
        // The control-table-resolved CURRENT slate is exempt from this shortcut, same reasoning
        // as EspnCacheService.GetWeekScoresAsync's identical NFL fix: SeasonWindowResolver can
        // legitimately keep an old slate as "current" well past its nominal end (the off-season
        // bootstrap case), and cfbAdapter.ts's current-slate path now always calls this fetcher
        // for that resolved slate — it needs the slate's real live/final ESPN state, not a DB
        // reconstruction with no live situation/clock data.
        using var scope = scopeFactory.CreateScope();
        var currentSlateService = scope.ServiceProvider.GetRequiredService<ICfbCurrentSlateService>();
        var currentSlate = await currentSlateService.GetCurrentSlateAsync();
        var isCurrentSlate = currentSlate?.Id == slate.Id;

        var slateHasEnded = !isCurrentSlate && slate.EndDate.ToDateTime(TimeOnly.MaxValue).AddHours(6) < DateTime.UtcNow;
        if (slateHasEnded) {
            var cacheKey = $"cfb-slate-scores_{slate.Id}";
            if (settledCache.TryGetValue<EspnScores>(cacheKey, out var cached)) return cached;

            var cfbRepo = scope.ServiceProvider.GetRequiredService<ICfbRepository>();
            var rows = (await cfbRepo.GetScoresForSlateAsync(slate.Id)).ToList();
            if (rows.Count > 0) {
                var games = rows.Select(row => new FinalScoresEspnMapper.FinishedGame(
                    row.Id.ToString(), row.HomeTeam, row.AwayTeam, row.HomeTeamScore, row.AwayTeamScore, row.GameTime,
                    row.WeatherDisplayValue is null && row.WeatherConditionId is null && row.WeatherTemperatureF is null
                        ? null
                        : new FinalScoresEspnMapper.WeatherInfo(row.WeatherDisplayValue, row.WeatherConditionId, row.WeatherTemperatureF)));
                var built = FinalScoresEspnMapper.Build(games, slate.Season, slate.EspnWeekNumber.Value, CfbSlateHelpers.IsCfpSlate(slate.ScoringFormat));
                settledCache.Set(cacheKey, built);
                return built;
            }
        }

        var result = CfbSlateHelpers.IsCfpSlate(slate.ScoringFormat)
            ? await FetchCfpAsync(slate)
            : await FetchRankedWeekAsync(slate);

        // Replay mode only (DEMO_REPLAY_MODE=true) — ReplayCacheService is a Singleton registered
        // only then, so this resolves null (a no-op) in every other environment. The replay
        // game (IND @ ATL) is seeded as a second game inside this real slate specifically so it
        // can be surfaced through CFB's normal slate-based flow (see
        // DemoDataSeeder.SeedReplayCfbSlateAsync) — but cfbApi above has no knowledge of it (it
        // only ever calls the real ESPN endpoints), so it has to be merged in here rather than
        // fetched as part of the real ESPN response.
        if (isCurrentSlate) {
            var replayService = scope.ServiceProvider.GetService<ReplayCacheService>();
            var replaySnapshot = replayService is null ? null : await replayService.GetScoresAsync();
            if (replaySnapshot?.Events is { Length: > 0 } replayEvents) {
                result = result is null
                    ? replaySnapshot
                    : new EspnScores {
                        Leagues = result.Leagues,
                        Season = result.Season,
                        Week = result.Week,
                        Events = [.. result.Events ?? [], .. replayEvents],
                    };
            }
        }

        return result;
    }

    private async Task<EspnScores?> FetchCfpAsync(CfbSlates slate) {
        // CFP: week=999 ESPN bucket returns all CFP games; filter to this round by date
        var scoreboard = await cfbApi.GetCfpGamesAsync();
        if (scoreboard?.Events is null) return null;

        var events = scoreboard.Events.Where(e => {
            var comp = e.Competitions.FirstOrDefault();
            return comp is not null
                && comp.Date.Date >= slate.StartDate.ToDateTime(TimeOnly.MinValue).Date
                && comp.Date.Date <= slate.EndDate.ToDateTime(TimeOnly.MaxValue).Date;
        }).ToArray();

        return events.Length == 0 ? null : WithEvents(scoreboard, events);
    }

    private async Task<EspnScores?> FetchRankedWeekAsync(CfbSlates slate) {
        // Regular season / conf-champs: full FBS week, no rank filter (frizat-9m0) — every game is
        // persisted for the audit trail; CfbSpreadJob computes IsLeagueEligible from rank + day of
        // week separately, gating what's *served* to users without dropping data at ingestion.
        var scoreboard = await cfbApi.GetScoresByWeekAsync(slate.EspnWeekNumber!.Value, isPostSeason: false);
        if (scoreboard?.Events is null) return null;

        return scoreboard.Events.Length == 0 ? null : WithEvents(scoreboard, scoreboard.Events);
    }

    private static EspnScores WithEvents(EspnScores source, Event[] events) => new() {
        Leagues = source.Leagues,
        Season  = source.Season,
        Week    = source.Week,
        Events  = events,
    };
}
