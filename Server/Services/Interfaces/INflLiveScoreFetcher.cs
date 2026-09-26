using FourPlayWebApp.Server.Models.Data;
using FourPlayWebApp.Shared.Models;

namespace FourPlayWebApp.Server.Services.Interfaces;

/// <summary>
/// Resolves the live ESPN scoreboard for one NFL week — date-range query scoped to the week's own
/// control-table window (frizat-11t: NflSeasonWeekConfig.WeekStartDatetime/WeekEndDatetime, not
/// ESPN's own week=N bucket), mirrors ICfbLiveScoreFetcher exactly. Shared by NflScoresJob,
/// NflSpreadJob, and EspnCacheService so none of them trust ESPN's own week bucketing directly.
/// </summary>
public interface INflLiveScoreFetcher {
    Task<EspnScores?> FetchForWeekAsync(NflSeasonWeekConfig week);
}
