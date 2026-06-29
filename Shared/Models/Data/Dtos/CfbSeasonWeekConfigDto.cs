namespace FourPlayWebApp.Shared.Models.Data.Dtos;

public record CfbSeasonWeekConfigDto(
    int EspnWeekNumber,
    int IvLeagueWeekNumber,
    string WeekType,
    string ScoringFormat,
    bool InScopeIvLeague,
    DateOnly WeekStartDate,
    DateOnly WeekEndDate,
    string? Notes);
