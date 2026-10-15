using System.Security.Cryptography;
using System.Text.Json;
using FourPlayWebApp.Shared.Models;

namespace FourPlayWebApp.Server.Services;

/// <summary>
/// Shared change-detection fingerprint for EspnScores — used by both EspnCacheService (NFL) and
/// CfbCacheService (CFB) so "did anything change" is computed identically for both sports
/// (frizat-703.6 unification).
/// </summary>
public static class EspnScoresFingerprint
{
    // PeriodicRefreshCache only raises Changed — which drives the SSE push to the browser and the
    // live pick notifications — when this differs from the last poll. frizat-6u6: it's a hash of
    // the whole fetched week (already filtered to the week's own games), serialized with the same
    // settings ESPN's JSON was parsed with — so every field the app reads counts, with no
    // hand-kept list to forget a new one in (frizat-a2u's down/distance push gap was exactly that).
    // Fields ESPN sends that the model doesn't map never reach here, so they can't cause pushes.
    public static string Compute(EspnScores scores) =>
        Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(scores, EspnApiServiceJsonConverter.Settings)));
}
