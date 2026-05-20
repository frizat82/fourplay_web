namespace FourPlayWebApp.Server.Jobs;

internal static class CfbSlateHelpers {
    public static bool IsTop25Slate(string slateType) =>
        slateType == "RegularSeason" || slateType == "ConferenceChampionship";
}
