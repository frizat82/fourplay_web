namespace FourPlayWebApp.Server.Jobs;

internal static class CfbSlateHelpers {
    public static bool IsTop25Slate(string slateType) =>
        slateType == "RegularSeason" || slateType == "ConferenceChampionship";

    // Returns ESPN seasontype: 2 = regular/conf-champs, 3 = CFP postseason rounds
    public static bool IsCfpSlate(string? scoringFormat) =>
        scoringFormat is "NFLDivisional" or "NFLConference" or "NFLSuperBowl";
}
