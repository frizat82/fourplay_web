using FourPlayWebApp.Server.Models.Data;

namespace FourPlayWebApp.Server.Jobs;

// Shared by CfbSlateSeederJob (slate Label) and CfbSpreadScheduleSource (trigger Description) —
// one place for the IvLeagueWeekNumber/WeekType/ScoringFormat -> human label mapping.
internal static class CfbWeekLabelHelper {
    public static string LabelFromConfig(CfbSeasonWeekConfig cfg) => cfg.WeekType switch {
        "Conference Championships" => "Conf. Championships",
        "FBS Playoff" => cfg.ScoringFormat switch {
            "NFLDivisional" when cfg.IvLeagueWeekNumber == 15 => "CFP First Round",
            "NFLDivisional" => "CFP Quarterfinals",
            "NFLConference" => "CFP Semifinals",
            "NFLSuperBowl"  => "CFP Championship",
            _ => $"CFP Week {cfg.IvLeagueWeekNumber}",
        },
        _ => $"Week {cfg.IvLeagueWeekNumber}",
    };
}
