namespace FourPlayWebApp.Shared.Models.Data.Dtos;

// One row of a sport's full-season spread-lock schedule (Rules page) — shared shape for both
// NFL and CFB, each sport's controller maps its own config entity into this.
public record SpreadLockWeekDto(string WeekLabel, DateTime SpreadLockDatetime);
