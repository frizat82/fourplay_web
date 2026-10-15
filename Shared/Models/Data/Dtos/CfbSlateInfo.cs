namespace FourPlayWebApp.Shared.Models.Data.Dtos;

// One CFB slate (an in-scope control-table row, frizat-b4g) as both /api/cfb/slates and
// /api/cfb/current-slate serve it — the client's one CfbSlateDto type. Built from the row by
// CfbSlateHelpers.ToSlateInfo.
public record CfbSlateInfo(int Id, int Season, int SlateNumber, string Label,
    DateOnly StartDate, DateOnly EndDate, DateTime SpreadLockDatetime,
    bool IsPostSeason, int RequiredPicks);
