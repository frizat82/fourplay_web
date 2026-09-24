using FourPlayWebApp.Server.Models.Data;
using FourPlayWebApp.Shared.Models.Enum;

namespace FourPlayWebApp.Server.Services;

/// <summary>
/// Which of a league's configured teases applies to a period (NFL week / CFB slate). One rule for
/// both sports; only each sport's round boundaries differ, and they're data here, not a second
/// copy of the logic (previously SpreadCalculator.ResolveNflJuice and
/// CfbLeaderboardService.JuiceForSlate).
/// </summary>
public static class JuiceTiers {
    private sealed record Boundaries(int FirstDivisional, int FirstConference, int? NoTeaseFrom);

    private static readonly Boundaries Nfl = new(FirstDivisional: 19, FirstConference: 21, NoTeaseFrom: 22);
    private static readonly Boundaries Cfb = new(FirstDivisional: 15, FirstConference: 17, NoTeaseFrom: null);

    public static double For(LeagueType sport, int period, LeagueJuiceMapping mapping) {
        var b = sport == LeagueType.Cfb ? Cfb : Nfl;
        if (b.NoTeaseFrom is int noTease && period >= noTease) return 0; // NFL Super Bowl
        if (period >= b.FirstConference) return mapping.JuiceConference;
        if (period >= b.FirstDivisional) return mapping.JuiceDivisional;
        return mapping.Juice;
    }
}
