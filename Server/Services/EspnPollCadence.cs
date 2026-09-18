using FourPlayWebApp.Shared.Models;

namespace FourPlayWebApp.Server.Services;

/// <summary>
/// Shared ESPN scoreboard poll cadence for EspnCacheService (NFL) and CfbCacheService (CFB) —
/// frizat-ucv. One shared value each, not two independently-tuned copies (CLAUDE.md's NFL/CFB
/// sharing rule), since there's no genuine sport-specific reason for these to ever diverge.
///
/// ESPN's real live-game duration varies (OT, delays) — 4h is a generous upper bound so the fast
/// poll doesn't drop out mid-game, at the cost of up to ~30min of unnecessary fast polling after a
/// rare long game ends. Fast/slow chosen to keep a single shared poll (this cache fans out to
/// every client, not one poll per viewer) feeling near-live without hammering ESPN's unofficial,
/// undocumented-rate-limit scoreboard endpoint the rest of the day.
/// </summary>
public static class EspnPollCadence
{
    public static readonly TimeSpan LiveGameDuration = TimeSpan.FromHours(4);
    public static readonly TimeSpan FastPollInterval = TimeSpan.FromSeconds(30);
    public static readonly TimeSpan SlowPollInterval = TimeSpan.FromMinutes(5);

    // Both EspnCacheService and CfbCacheService cache the identical EspnScores shape (NFL/CFB
    // scoreboards share one ESPN wire format) — one extraction, not two copies.
    public static IEnumerable<DateTimeOffset> KickoffTimes(EspnScores scores) =>
        scores.Events?.SelectMany(e => e.Competitions).Select(c => c.Date) ?? [];
}
