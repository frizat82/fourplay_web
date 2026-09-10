using FourPlayWebApp.Shared.Models;
using FourPlayWebApp.Shared.Models.Enum;

namespace FourPlayWebApp.Server.Services;

/// <summary>
/// Shared change-detection fingerprint for EspnScores — used by both EspnCacheService (NFL) and
/// CfbCacheService (CFB) so "did the score/status change" is computed identically for both sports
/// (frizat-703.6 unification).
/// </summary>
public static class EspnScoresFingerprint
{
    // frizat-a2u (fast-follow, 2026-09-10): PeriodicRefreshCache only raises Changed — which
    // drives the SSE push to the browser — when this fingerprint differs from the last poll.
    // Score+status alone missed every non-scoring play in a live game (down/distance, yard line,
    // possession, clock all keep changing while the score doesn't), so the push silently never
    // fired for most of a game's actual live-game changes. Include everything a viewer can see
    // on screen (Scores page status line + FieldPosition), not just the scoreboard.
    public static string Compute(EspnScores scores) =>
        string.Join("|", scores.Events?.Select(e => {
            var c = e.Competitions.FirstOrDefault();
            var home = c?.Competitors.FirstOrDefault(x => x.HomeAway == HomeAway.Home);
            var away = c?.Competitors.FirstOrDefault(x => x.HomeAway == HomeAway.Away);
            var sit = c?.Situation;
            // sit?.Possession (team id) is the authoritative field — GameSituationDto.FromSituation
            // and GameHelpers.GetPossessionTeamAbbr both key off it, not the derived display string
            // possessionText — hash the same field the rest of the app treats as source-of-truth.
            return $"{e.Id}:{home?.Score}:{away?.Score}:{c?.Status.Type.Name}:{c?.Status.DisplayClock}:{c?.Status.Period}:{sit?.Down}:{sit?.YardLine}:{sit?.Distance}:{sit?.Possession}:{sit?.IsRedZone}";
        }) ?? []);
}
