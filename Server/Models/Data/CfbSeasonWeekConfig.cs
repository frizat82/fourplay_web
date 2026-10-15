using System.ComponentModel.DataAnnotations.Schema;
using FourPlayWebApp.Shared.Models.Enum;

namespace FourPlayWebApp.Server.Models.Data;

/// <summary>
/// The CFB control table — one row per week of the season, and the ONLY CFB schedule table
/// (frizat-b4g): an in-scope row IS a slate. CfbSpreads/CfbScores/CfbPicks point at its Id
/// (their CfbSlateId). Same shape as <see cref="NflSeasonWeekConfig"/>.
/// </summary>
public class CfbSeasonWeekConfig {
    public int Id { get; set; }
    public int Season { get; set; }
    // Our CFB week = the slate number (1-18). Null for a week we don't bet on (InScopeIvLeague false).
    public int? IvLeagueWeekNumber { get; set; }
    public DateOnly WeekStartDate { get; set; }
    public DateOnly WeekEndDate { get; set; }
    public WeekType WeekType { get; set; }
    public string WeekLabel { get; set; } = string.Empty;   // "Week 5", "CFP Semifinals" — what every screen and push calls this round (frizat-d47)
    public ScoringFormat ScoringFormat { get; set; }
    // Whether the week is bet on at all — the one skip flag (a skipped week has no week number).
    public bool InScopeIvLeague { get; set; }
    public DateTime SpreadLockDatetime { get; set; }        // UTC — when CfbSpreadSchedulerJob fires CfbSpreadJob for this week; required, never null
    public string? Notes { get; set; }

    // frizat-d47: the round's rules (see NflSeasonWeekConfig). A skipped week carries 0 / false.
    public bool IsPostSeason { get; set; }
    public int RequiredPicks { get; set; }

    /// <summary>The slate number of an in-scope week — every slate read returns only those.</summary>
    [NotMapped]
    public int SlateNumber => IvLeagueWeekNumber
        ?? throw new InvalidOperationException($"CfbSeasonWeekConfigs {Id} (season {Season}) is not an in-scope week and has no slate number.");
}
