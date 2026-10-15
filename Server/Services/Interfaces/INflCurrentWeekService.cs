using FourPlayWebApp.Shared.Models.Enum;

namespace FourPlayWebApp.Server.Services.Interfaces;

// No EspnWeek field — every consumer (backend and frontend) now routes purely by WeekId (our
// own control table), never ESPN's own numbering. See frizat-3nv.
// IsPostSeason/RequiredPicks are the week's control-table row values (frizat-d47).
public record NflWeekInfo(int WeekId, int Season, bool IsPostSeason, string WeekLabel, ScoringFormat ScoringFormat, DateTime SpreadLockDatetime, int RequiredPicks);

public interface INflCurrentWeekService {
    Task<NflWeekInfo> GetCurrentWeekAsync();

    // Season-level (not week-level) check: is a season actually happening right now, at all?
    // Callers that only need this yes/no answer (the ESPN cache poller) should use this instead
    // of re-deriving it externally — this service already owns the row-fetch + window-mapping.
    Task<bool> IsSeasonActiveAsync();
}
