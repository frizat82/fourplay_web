namespace FourPlayWebApp.Server.Data.Configurations;

// frizat-b4g: one ScoringFormat -> RequiredPicks mapping, enforced identically on both control
// tables, so a week's tier and its pick count can never disagree.
internal static class WeekRuleChecks {
    public const string ScoringFormatMatchesRequiredPicks =
        "(\"ScoringFormat\" = 'Standard' AND \"RequiredPicks\" = 4) OR " +
        "(\"ScoringFormat\" = 'Divisional' AND \"RequiredPicks\" = 3) OR " +
        "(\"ScoringFormat\" = 'Conference' AND \"RequiredPicks\" = 2) OR " +
        "(\"ScoringFormat\" = 'Final' AND \"RequiredPicks\" = 1) OR " +
        "(\"ScoringFormat\" = 'None' AND \"RequiredPicks\" = 0)";
}
