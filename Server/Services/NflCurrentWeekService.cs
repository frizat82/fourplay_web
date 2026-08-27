using FourPlayWebApp.Server.Services.Interfaces;
using FourPlayWebApp.Server.Services.Repositories.Interfaces;

namespace FourPlayWebApp.Server.Services;

public class NflCurrentWeekService(ILeagueRepository repo) : INflCurrentWeekService {
    public async Task<NflWeekInfo> GetCurrentWeekAsync() {
        var now = DateTime.UtcNow;
        var configs = await repo.GetNflSeasonWeekConfigsAsync();

        // frizat-9xg: most recent week whose own spread grab has passed, unless we're within
        // 2 days of the next week's spread grab — see SeasonWindowResolver for the shared
        // NFL/CFB logic (applies identically across a season boundary, no special case here).
        var windows = configs.Select(c => new SeasonWindowResolver.WeekWindow(c.Season, c.WeekStartDatetime, c.WeekEndDatetime, c.SpreadLockDatetime));
        var resolved = SeasonWindowResolver.ResolveCurrentWeek(windows, now)
            ?? throw new InvalidOperationException("No NflSeasonWeekConfig rows exist — cannot resolve a current week.");

        var config = configs.First(c => c.Season == resolved.Season
            && c.WeekStartDatetime == resolved.Start && c.WeekEndDatetime == resolved.End);
        return ToWeekInfo(config);
    }

    public async Task<bool> IsSeasonActiveAsync() {
        var configs = await repo.GetNflSeasonWeekConfigsAsync();
        var windows = configs.Select(c => new SeasonWindowResolver.Window(c.Season, c.WeekStartDatetime, c.WeekEndDatetime));
        return SeasonWindowResolver.IsSeasonActive(windows, DateTime.UtcNow);
    }

    private static NflWeekInfo ToWeekInfo(Models.Data.NflSeasonWeekConfig cfg) {
        var isPostSeason = cfg.WeekId > 18;
        var espnWeek = cfg.WeekId switch {
            <= 18 => cfg.WeekId,
            19    => 1,  // Wild Card
            20    => 2,  // Divisional
            21    => 3,  // Conference Championship
            22    => 5,  // Super Bowl (ESPN skips week 4 = Pro Bowl)
            _     => cfg.WeekId
        };
        return new NflWeekInfo(cfg.WeekId, espnWeek, cfg.Season, isPostSeason, cfg.WeekLabel, cfg.ScoringFormat, cfg.SpreadLockDatetime);
    }
}
