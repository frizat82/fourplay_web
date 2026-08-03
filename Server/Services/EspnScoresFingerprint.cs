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
    public static string Compute(EspnScores scores) =>
        string.Join("|", scores.Events?.Select(e => {
            var c = e.Competitions.FirstOrDefault();
            var home = c?.Competitors.FirstOrDefault(x => x.HomeAway == HomeAway.Home);
            var away = c?.Competitors.FirstOrDefault(x => x.HomeAway == HomeAway.Away);
            return $"{e.Id}:{home?.Score}:{away?.Score}:{c?.Status.Type.Name}";
        }) ?? []);
}
