using FourPlayWebApp.Server.Models.Data;

namespace FourPlayWebApp.Server.Services;

/// <summary>
/// Which of a league's configured teases applies to a period (NFL week / CFB slate), keyed on the
/// round's pick count from its control-table row (RequiredPicks, frizat-d47) — the same value
/// that caps picks, so the two can't drift: 4 picks = regular tease, 3 = divisional,
/// 2 = conference, 1 (the final — NFL Super Bowl, CFP championship) = no tease.
/// </summary>
public static class JuiceTiers {
    public static double For(int requiredPicks, LeagueJuiceMapping mapping) => requiredPicks switch {
        >= 4 => mapping.Juice,
        3 => mapping.JuiceDivisional,
        2 => mapping.JuiceConference,
        _ => 0,
    };
}
