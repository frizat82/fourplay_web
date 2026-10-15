using FourPlayWebApp.Server.Models.Data;

namespace FourPlayWebApp.Server.Services.Interfaces;

// One CFB slate (an in-scope control-table row, frizat-b4g) as both /api/cfb/slates and
// /api/cfb/current-slate serve it — the client's one CfbSlateDto type.
public record CfbSlateInfo(int Id, int Season, int SlateNumber, string Label,
    DateOnly StartDate, DateOnly EndDate, DateTime SpreadLockDatetime,
    bool IsPostSeason, int RequiredPicks) {
    public static CfbSlateInfo From(CfbSeasonWeekConfig s) => new(s.Id, s.Season, s.SlateNumber, s.WeekLabel,
        s.WeekStartDate, s.WeekEndDate, s.SpreadLockDatetime, s.IsPostSeason, s.RequiredPicks);
}

public interface ICfbCurrentSlateService {
    Task<CfbSlateInfo?> GetCurrentSlateAsync();

    // Season-level (not slate-level) check: is a season actually happening right now, at all?
    // Callers that only need this yes/no answer (the ESPN cache poller) should use this instead
    // of re-deriving it externally — this service already owns the row-fetch + window-mapping.
    Task<bool> IsSeasonActiveAsync();
}
