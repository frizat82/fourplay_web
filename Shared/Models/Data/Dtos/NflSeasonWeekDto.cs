namespace FourPlayWebApp.Shared.Models.Data.Dtos;

// One NFL week of a season as its NflSeasonWeekConfigs row defines it (frizat-d47). nflAdapter
// reads a past week's RequiredPicks from here; the label and postseason flag are the row's too,
// for building the week selector from the table rather than constants.
public record NflSeasonWeekDto(int WeekId, string WeekLabel, bool IsPostSeason, int RequiredPicks);
