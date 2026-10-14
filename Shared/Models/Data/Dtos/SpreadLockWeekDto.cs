namespace FourPlayWebApp.Shared.Models.Data.Dtos;

// One row of a sport's full-season spread-lock schedule (Rules page) — shared shape for both
// NFL and CFB, each sport's controller maps its own config entity into this. IsPostSeason and
// RequiredPicks are the row's own control-table values (frizat-d47): the Rules page's per-round
// pick counts come from here, not from a rule baked into the page.
public record SpreadLockWeekDto(string WeekLabel, DateTime SpreadLockDatetime, bool IsPostSeason, int RequiredPicks);
