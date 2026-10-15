using FourPlayWebApp.Shared.Models.Enum;

namespace FourPlayWebApp.Server.Models.Data;

/// <summary>
/// frizat-b4g: what both sports' control-table rows share — one row per week, carrying its own
/// rules. EF configures and checks these the same way for both tables (WeekRuleChecks).
/// </summary>
public interface IControlTableWeek {
    WeekType WeekType { get; }
    ScoringFormat ScoringFormat { get; }
    bool InScopeIvLeague { get; }
    bool IsPostSeason { get; }
    int RequiredPicks { get; }
}
