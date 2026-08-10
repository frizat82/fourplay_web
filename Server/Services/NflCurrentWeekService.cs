using FourPlayWebApp.Server.Services.Interfaces;
using FourPlayWebApp.Server.Services.Repositories.Interfaces;

namespace FourPlayWebApp.Server.Services;

public class NflCurrentWeekService(ILeagueRepository repo) : INflCurrentWeekService {
    public async Task<NflWeekInfo> GetCurrentWeekAsync() {
        var now = DateTime.UtcNow;
        var configs = await repo.GetNflSeasonWeekConfigsAsync();

        var active = configs.FirstOrDefault(c => c.WeekStartDatetime <= now && now <= c.WeekEndDatetime);

        // Off-season: return most recent completed week
        active ??= configs.Where(c => c.WeekEndDatetime < now)
                          .OrderByDescending(c => c.WeekEndDatetime)
                          .FirstOrDefault();

        // Pre-season: return upcoming Week 1
        active ??= configs.Where(c => c.WeekStartDatetime > now)
                          .OrderBy(c => c.WeekStartDatetime)
                          .First();

        return ToWeekInfo(active);
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
