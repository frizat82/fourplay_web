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
    public DateTime SpreadLockDatetime { get; set; }        // UTC — when CfbSpreadSchedulerJob fires CfbSpreadJob for this week; required, never null
    public string? Notes { get; set; }

    // frizat-d47: the round's rules (see NflSeasonWeekConfig). CFB code reads them off CfbSlates,
    // which CfbRepository fills from this row on every read. Excluded weeks (99) carry 0 / false.
    public bool IsPostSeason { get; set; }
    public int RequiredPicks { get; set; }
}
