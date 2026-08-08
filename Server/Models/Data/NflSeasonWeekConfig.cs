namespace FourPlayWebApp.Server.Models.Data;

public class NflSeasonWeekConfig {
    public int Id { get; set; }
    public int Season { get; set; }
    public int WeekId { get; set; }          // canonical 1-22 (1-18 regular, 19=WC, 20=Div, 21=CC, 22=SB)
    public string WeekLabel { get; set; } = string.Empty;
    public string WeekType { get; set; } = string.Empty;
    public string ScoringFormat { get; set; } = string.Empty;
    public DateTime WeekStartDatetime { get; set; }
    public DateTime WeekEndDatetime { get; set; }
    public DateTime? FirstGameOfWeekStartDatetime { get; set; }
    public DateTime? SpreadLockDatetime { get; set; }    // UTC — when NflSpreadSchedulerJob fires NflSpreadJob for this week
    public string? WeekNotes { get; set; }
}
