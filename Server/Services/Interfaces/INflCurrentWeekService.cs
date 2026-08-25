namespace FourPlayWebApp.Server.Services.Interfaces;

public record NflWeekInfo(int WeekId, int EspnWeek, int Season, bool IsPostSeason, string WeekLabel, string ScoringFormat, DateTime SpreadLockDatetime);

public interface INflCurrentWeekService {
    Task<NflWeekInfo> GetCurrentWeekAsync();

    // Season-level (not week-level) check: is a season actually happening right now, at all?
    // Callers that only need this yes/no answer (the ESPN cache poller) should use this instead
    // of re-deriving it externally — this service already owns the row-fetch + window-mapping.
    Task<bool> IsSeasonActiveAsync();
}
