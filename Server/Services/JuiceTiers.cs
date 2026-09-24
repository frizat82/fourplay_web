using FourPlayWebApp.Server.Models.Data;
using FourPlayWebApp.Shared.Helpers;
using FourPlayWebApp.Shared.Models.Enum;

namespace FourPlayWebApp.Server.Services;

/// <summary>
/// Which of a league's configured teases applies to a period (NFL week / CFB slate). The round a
/// period belongs to is read from the required-pick table (GameHelpers) — the one place round
/// boundaries live — so the two can't drift: 4 picks = regular tease, 3 = divisional, 2 =
/// conference. The final (1 pick) is the only sport difference: NFL's Super Bowl has no tease, CFB's
/// championship keeps the conference tease.
/// </summary>
public static class JuiceTiers {
    public static double For(LeagueType sport, int period, LeagueJuiceMapping mapping) {
        var requiredPicks = sport == LeagueType.Cfb ? GameHelpers.GetCfbRequiredPicks(period) : GameHelpers.GetRequiredPicks(period);
        return requiredPicks switch {
            >= 4 => mapping.Juice,
            3 => mapping.JuiceDivisional,
            2 => mapping.JuiceConference,
            _ => sport == LeagueType.Cfb ? mapping.JuiceConference : 0,
        };
    }
}
