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
    private readonly IEspnApiService _espnApiService;
    private readonly ILeagueRepository _leagueRepository;
    private readonly IMemoryCache _historicalCache;
    private readonly PeriodicRefreshCache<EspnScores> _cache;

    public event Action? ScoresChanged
    {
        add => _cache.Changed += value;
        remove => _cache.Changed -= value;
    }

    public EspnCacheService(IEspnApiService espnApiService, INflCurrentWeekService nflCurrentWeekService, ILeagueRepository leagueRepository, IMemoryCache historicalCache, TimeSpan? initialDelay = null)
    {
        _espnApiService = espnApiService;
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
                return await espnApiService.GetWeekScores(week.EspnWeek, week.Season, week.IsPostSeason);
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
    public async Task<EspnScores?> GetWeekScoresAsync(int week, int year, bool postSeason = false)
    {
        var cacheKey = $"nfl-week-scores_{year}_{week}_{postSeason}";
        if (_historicalCache.TryGetValue<EspnScores>(cacheKey, out var cached)) return cached;

        var nflWeek = GameHelpers.GetWeekFromEspnWeek(week, postSeason);
        var rows = await _leagueRepository.GetNflScoresAsync(year, nflWeek);
        if (rows.Count > 0) {
            var games = rows.Select(row => new FinalScoresEspnMapper.FinishedGame(
                row.Id.ToString(), row.HomeTeam, row.AwayTeam, row.HomeTeamScore, row.AwayTeamScore, row.GameTime));
            var built = FinalScoresEspnMapper.Build(games, year, week, postSeason);
            _historicalCache.Set(cacheKey, built);
            return built;
        }
        return await _espnApiService.GetWeekScores(week, year, postSeason);
    }

    public ValueTask DisposeAsync() => _cache.DisposeAsync();
}
