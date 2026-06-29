namespace FourPlayWebApp.Server.Models.Data;

public class CfbSeasonWeekConfig {
    public int Id { get; set; }
    public int Season { get; set; }
    public int EspnWeekNumber { get; set; }
    public int IvLeagueWeekNumber { get; set; }    // 1-18 = slate number; 99 = excluded
    public DateOnly WeekStartDate { get; set; }
    public DateOnly WeekEndDate { get; set; }
    public string WeekType { get; set; } = string.Empty;
    public string ScoringFormat { get; set; } = string.Empty;
    public bool InScopeIvLeague { get; set; }
    public string? Notes { get; set; }
}
