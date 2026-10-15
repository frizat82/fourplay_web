using FourPlayWebApp.Server.Jobs;
using FourPlayWebApp.Server.Models.Data;
using FourPlayWebApp.Server.Services.Interfaces;
using FourPlayWebApp.Shared.Helpers;
using FourPlayWebApp.Shared.Models;
using Serilog;

namespace FourPlayWebApp.Server.Services;

// Pure ESPN fetch, no caching (frizat-d0t) — mirrors NflLiveScoreFetcher's shape exactly. The
// settled-slate DB-reconstruction + cache that used to live here moved to
// CfbCacheService.GetSlateScoresAsync, matching where NFL's equivalent already lived
// (EspnCacheService.GetWeekScoresAsync) — this fetcher is now called directly by CfbScoresJob
// (no bypassCache flag needed anymore, since there's no cache here to bypass) and by
// CfbCacheService as the live-fallback delegate.
//
// ReplayCacheService is a Singleton, registered only when DEMO_REPLAY_MODE=true — resolved via
// IServiceProvider.GetService (returns null gracefully when unregistered) rather than a DI scope,
// since a Singleton needs no scope to resolve safely from a Singleton caller.
public class CfbLiveScoreFetcher(ICfbApiService cfbApi, IServiceProvider serviceProvider) : ICfbLiveScoreFetcher {
    public async Task<EspnScores?> FetchForSlateAsync(CfbSeasonWeekConfig slate, bool isCurrentSlate) {
        // frizat-b4g: everything this needs is the slate's own control-table row — its dates, and
        // whether it's a CFP round (WeekType Playoff). No ESPN week number is involved.
        var result = CfbSlateHelpers.IsCfpSlate(slate)
            ? await FetchCfpAsync(slate)
            : await FetchRegularSeasonAsync(slate);

        // Replay mode only (DEMO_REPLAY_MODE=true) — ReplayCacheService resolves null (a no-op)
        // in every other environment. The replay game (IND @ ATL) is seeded as a second game
        // inside this real slate specifically so it can be surfaced through CFB's normal
        // slate-based flow (see DemoDataSeeder.SeedReplayCfbSlateAsync) — but cfbApi above has no
        // knowledge of it (it only ever calls the real ESPN endpoints), so it has to be merged in
        // here rather than fetched as part of the real ESPN response. Only merged for the
        // CURRENT slate — a historical/other slate has no business surfacing the replay fixture.
        if (isCurrentSlate) {
            var replayService = serviceProvider.GetService<ReplayCacheService>();
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

    private async Task<EspnScores?> FetchCfpAsync(CfbSeasonWeekConfig slate) {
        // CFP: week=999 ESPN bucket returns all CFP games; filter to this round by date
        var scoreboard = await cfbApi.GetCfpGamesAsync();
        if (scoreboard?.Events is null) return null;

        var events = GameHelpers.FilterEventsToDateWindow(scoreboard.Events,
            slate.WeekStartDate.ToDateTime(TimeOnly.MinValue), slate.WeekEndDate.ToDateTime(TimeOnly.MaxValue));

        return events.Length == 0 ? null : GameHelpers.WithEvents(scoreboard, events);
    }

    private async Task<EspnScores?> FetchRegularSeasonAsync(CfbSeasonWeekConfig slate) {
        // Regular season / conf-champs: full FBS slate, no rank filter (frizat-9m0) — every game is
        // persisted for the audit trail; CfbSpreadJob computes IsLeagueEligible from rank + day of
        // week separately, gating what's *served* to users without dropping data at ingestion.
        //
        // frizat-11t: queries ESPN by our own week's dates, not an ESPN week number —
        // ESPN's week=N scoreboard bucket doesn't respect our slate's date boundaries (e.g. a team's
        // early "week 0" opener can land in the same week=N response as their real week-N game),
        // which silently broke the "one game per team per slate" assumption the frontend's live-score
        // join relies on. Scoping the ESPN call to our own control-table dates instead makes that
        // assumption hold in practice (verified live). The downstream filter below is defense in
        // depth on top of that, not a substitute for it — /code-review's point: this fix's whole
        // premise is "don't fully trust ESPN's own bucketing," so don't trust the dates= query param
        // to be honored perfectly either (e.g. a timezone-boundary edge case on a late-night/West
        // Coast kickoff). Same date-window filter FetchCfpAsync already applies above.
        var scoreboard = await cfbApi.GetScoresByDateRangeAsync(slate.WeekStartDate, slate.WeekEndDate);
        if (scoreboard?.Events is null) return null;

        var events = GameHelpers.FilterEventsToDateWindow(scoreboard.Events,
            slate.WeekStartDate.ToDateTime(TimeOnly.MinValue), slate.WeekEndDate.ToDateTime(TimeOnly.MaxValue));

        return events.Length == 0 ? null : GameHelpers.WithEvents(scoreboard, events);
    }
}
