namespace FourPlayWebApp.Shared.Models.Data.Dtos;

// GET /api/cfb/slates/{season} — one CFB slate (an in-scope control-table week, frizat-b4g) as the
// client's CfbSlateDto type sees it: its id (what spreads/scores/picks point at), number, label,
// dates and rules.
public record CfbSlateDto(int Id, int Season, int SlateNumber, string Label, DateOnly StartDate, DateOnly EndDate,
    bool IsPostSeason, int RequiredPicks);
