using FourPlayWebApp.Shared.Models;

namespace FourPlayWebApp.Server.Services.Interfaces;

public interface IEspnApiService {
    // Date-range query, scoped to our own control table's WeekStartDatetime/WeekEndDatetime —
    // mirrors ICfbApiService.GetScoresByDateRangeAsync (frizat-11t). Never trust ESPN's own
    // week=N bucketing: it doesn't respect our control table's date windows (a rescheduled game
    // can land in the wrong week's response), so every caller queries by date and buckets the
    // result against its own control-table row instead.
    public Task<EspnScores?> GetScoresByDateRangeAsync(DateOnly startDate, DateOnly endDate, bool postSeason = false);
}
