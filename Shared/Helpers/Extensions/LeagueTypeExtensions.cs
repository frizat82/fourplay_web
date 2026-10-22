using FourPlayWebApp.Shared.Models.Enum;

namespace FourPlayWebApp.Shared.Helpers.Extensions;

public static class LeagueTypeExtensions
{
    /// <summary>The sport's short name as people see it: "NFL" or "CFB".</summary>
    public static string Label(this LeagueType sport) => sport == LeagueType.Cfb ? "CFB" : "NFL";
}
