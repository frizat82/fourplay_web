using FourPlayWebApp.Shared.Models.Enum;

namespace FourPlayWebApp.Server.Models.Data;

public class NflSeasonWeekConfig : IControlTableWeek {
    public int Id { get; set; }
    public int Season { get; set; }
    public int WeekId { get; set; }          // canonical 1-22 (1-18 regular, 19=WC, 20=Div, 21=CC, 22=SB)
    public string WeekLabel { get; set; } = string.Empty;
    public WeekType WeekType { get; set; }
    public ScoringFormat ScoringFormat { get; set; }
    // Whether the week is bet on — same skip flag as CfbSeasonWeekConfig (every NFL week today).
    public bool InScopeIvLeague { get; set; } = true;
    public DateTime WeekStartDatetime { get; set; }
    public DateTime WeekEndDatetime { get; set; }
    public DateTime? FirstGameOfWeekStartDatetime { get; set; }
    public DateTime SpreadLockDatetime { get; set; }    // UTC — when NflSpreadSchedulerJob fires NflSpreadJob for this week; required, never null
    public string? WeekNotes { get; set; }

    // frizat-d47: the round's rules live on its row, not in code keyed on WeekId. RequiredPicks
    // drives the AddPicks cap, the leaderboard's incomplete-week check and the tease tier
    // (JuiceTiers); IsPostSeason is the app's postseason (O/U offered, the Postseason selector).
    // It is NOT ESPN's season type in general: CFB conference championships are postseason here
    // but ESPN season type 2 (a CFB week's WeekType decides the ESPN query). For NFL the two
    // happen to coincide, so the NFL ESPN fetch passes it straight through.
    public bool IsPostSeason { get; set; }
    public int RequiredPicks { get; set; }
}
