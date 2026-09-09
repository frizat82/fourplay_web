using FourPlayWebApp.Server.Services.Interfaces;
using FourPlayWebApp.Server.Services.Repositories.Interfaces;
using FourPlayWebApp.Shared.Helpers;
using FourPlayWebApp.Shared.Models;
using Microsoft.Extensions.Caching.Memory;

namespace FourPlayWebApp.Server.Services;

// Service that periodically refreshes NFL scores from ESPN and caches them in memory, so
// concurrent controller requests share one poll instead of each triggering its own ESPN call.
// Wraps the shared PeriodicRefreshCache engine (frizat-703.6) — CfbCacheService uses the same
// engine for CFB, differing only in what/how it fetches.
public class EspnCacheService : IEspnCacheService, IAsyncDisposable
{
    private readonly INflLiveScoreFetcher _fetcher;
    private readonly ILeagueRepository _leagueRepository;
    private readonly IMemoryCache _historicalCache;
    private readonly PeriodicRefreshCache<EspnScores> _cache;

    public event Action? ScoresChanged
    {
        add => _cache.Changed += value;
        remove => _cache.Changed -= value;
    }

    public EspnCacheService(INflLiveScoreFetcher fetcher, INflCurrentWeekService nflCurrentWeekService, ILeagueRepository leagueRepository, IMemoryCache historicalCache, TimeSpan? initialDelay = null)
    {
        _fetcher = fetcher;
        _leagueRepository = leagueRepository;
        _historicalCache = historicalCache;
        _cache = new PeriodicRefreshCache<EspnScores>(
            fetch: async () => {
                // NflCurrentWeekService always resolves *something* now (most-recently-completed
                // or soonest-upcoming week, for UI-default purposes) — so its result alone can't
                // gate off-season ESPN polling. IsSeasonActiveAsync is the purpose-built,
                // season-level check for that (see SeasonWindowResolver).
                if (!await nflCurrentWeekService.IsSeasonActiveAsync()) return null;

                var week = await nflCurrentWeekService.GetCurrentWeekAsync();
                var configs = await leagueRepository.GetNflSeasonWeekConfigsAsync();
                var matchingConfig = configs.FirstOrDefault(c => c.Season == week.Season && c.WeekId == week.WeekId);
                return matchingConfig is null ? null : await _fetcher.FetchForWeekAsync(matchingConfig);
            },
            fingerprint: EspnScoresFingerprint.Compute,
            interval: TimeSpan.FromMinutes(5),
            initialDelay: initialDelay);
    }

    public Task<EspnScores?> GetScoresAsync() => Task.FromResult(_cache.Current);

    // Historical weeks are read straight from NflScores when persisted — every persisted row is
    // already FINAL (NflScoresJob only writes finished games), so 100 concurrent viewers of the
    // same past week share one DB read (and one build, cached indefinitely — settled data never
    // changes) instead of each triggering its own ESPN call or DB query. Only a week NflScoresJob
    // hasn't synced yet (or a genuinely current/future week — not this endpoint's real use, see
    // EspnController's doc comment) falls through to a live ESPN call.
    //
    // DB-first only kicks in once the week's own window has fully ended — /code-review caught
    // that this exact bug, already fixed for CFB in CfbLiveScoreFetcher (see its comment), had
    // not been ported here: gating purely on "any row persisted" means the instant one game in a
    // multi-game week finishes and gets persisted, the response is built from only that game and
    // cached forever — every other game in that week, including ones that finish and get
    // persisted later, is permanently dropped from every future response.
    public async Task<EspnScores?> GetWeekScoresAsync(int season, int nflWeek)
    {
        // Cache key is the resolved internal (season, WeekId) — matches
        // CfbLiveScoreFetcher's cfb-slate-scores_{slateId} shape and is what
        // InvalidateWeekCache(season, week) can actually address after an upsert.
        var cacheKey = $"nfl-week-scores_{season}_{nflWeek}";
        if (_historicalCache.TryGetValue<EspnScores>(cacheKey, out var cached)) return cached;

        // One unscoped fetch serves both purposes below — finding this week's own row and
        // resolving which week SeasonWindowResolver currently treats as "current" needs the
        // full set of configs either way.
        var allConfigs = await _leagueRepository.GetNflSeasonWeekConfigsAsync();
        var matchingConfig = allConfigs.FirstOrDefault(c => c.Season == season && c.WeekId == nflWeek);

        // The control-table-resolved CURRENT week is always live-fetched, even if its own
        // calendar window already looks "ended" by the 6-hour buffer below —
        // SeasonWindowResolver can legitimately keep an old window as "current" well past its
        // nominal end (e.g. the off-season bootstrap case: showing last season's Super Bowl
        // until 2 days before the next season's first spread grab), and the frontend's
        // current-week path (nflAdapter.ts) now always calls this endpoint for that resolved
        // week. Skipping this check would silently serve the "genuinely historical" DB-final-
        // score reconstruction below (no live situation/clock data) for the week the UI is
        // actively treating as current, instead of its real live/final ESPN state.
        var windows = allConfigs.Select(c => new SeasonWindowResolver.WeekWindow(c.Season, c.WeekStartDatetime, c.WeekEndDatetime, c.SpreadLockDatetime));
        var resolvedCurrent = SeasonWindowResolver.ResolveCurrentWeek(windows, DateTime.UtcNow);
        var isResolvedCurrentWeek = matchingConfig is not null && resolvedCurrent is not null
            && resolvedCurrent.Value.Season == matchingConfig.Season
            && resolvedCurrent.Value.Start == matchingConfig.WeekStartDatetime
            && resolvedCurrent.Value.End == matchingConfig.WeekEndDatetime;

        // 6-hour buffer past the configured end, mirroring CfbLiveScoreFetcher's identical
        // safety margin — this now gets cached forever once true, so a week whose config end
        // time turns out to be a little too tight against its actual last kickoff shouldn't
        // permanently freeze a still-in-progress game out of every future response.
        var weekHasEnded = !isResolvedCurrentWeek && matchingConfig is not null && matchingConfig.WeekEndDatetime.AddHours(6) < DateTime.UtcNow;

        if (weekHasEnded) {
            var rows = await _leagueRepository.GetNflScoresAsync(season, nflWeek);
            if (rows.Count > 0) {
                var games = rows.Select(row => new FinalScoresEspnMapper.FinishedGame(
                    row.Id.ToString(), row.HomeTeam, row.AwayTeam, row.HomeTeamScore, row.AwayTeamScore, row.GameTime));
                // NB: the built EspnScores.Week.Number below holds our internal nflWeek, not
                // ESPN's real week number the live-fetch path (_fetcher.FetchForWeekAsync) would
                // have put there — no current consumer reads this field from a live response for
                // a decision, but it's a real seam the full frizat-3nv GameData DTO would close.
                var built = FinalScoresEspnMapper.Build(games, season, nflWeek, matchingConfig!.WeekType == "PostSeason");
                _historicalCache.Set(cacheKey, built);
                return built;
            }
        }

        // Never trust ESPN's own week=N bucketing (frizat-11t's NFL mirror) — only fetch when we
        // have a real control-table row to scope the date-range query to. No matching config
        // means there's nothing to ask ESPN for.
        return matchingConfig is null ? null : await _fetcher.FetchForWeekAsync(matchingConfig);
    }

    public void InvalidateWeekCache(int season, int week) =>
        _historicalCache.Remove($"nfl-week-scores_{season}_{week}");

    public ValueTask DisposeAsync() => _cache.DisposeAsync();
}
