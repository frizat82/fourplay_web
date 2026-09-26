using FourPlayWebApp.Shared.Helpers;
using FourPlayWebApp.Shared.Models;

namespace FourPlayWebApp.Server.Services;

/// <summary>
/// Shared ESPN scoreboard poll cadence for EspnCacheService (NFL) and CfbCacheService (CFB) —
/// frizat-ucv. One shared value each, not two independently-tuned copies (CLAUDE.md's NFL/CFB
/// sharing rule), since there's no genuine sport-specific reason for these to ever diverge.
///
/// The pollers only run while a game is on (<see cref="IsLive"/>): Fast keeps the single shared
/// poll (this cache fans out to every client, not one poll per viewer) near-live during games, and
/// outside games ScorePollSchedule sleeps until the next kickoff. Finals are persisted by the
/// scores jobs, not re-polled. Slow is the retry cadence after a failed poll.
/// </summary>
public static class EspnPollCadence
{
    public static readonly TimeSpan FastPollInterval = TimeSpan.FromSeconds(15);
    public static readonly TimeSpan SlowPollInterval = TimeSpan.FromMinutes(5);
    // A game still "scheduled" this long after kickoff is postponed/canceled (ESPN keeps those
    // scheduled), not a delayed start.
    public static readonly TimeSpan LiveGameDuration = TimeSpan.FromHours(4);
    // A game that has started stays live until final (delays, OT) — for at most this long, so a
    // status stuck "in progress" at ESPN can't keep the pollers fast forever.
    public static readonly TimeSpan OverlongGameLimit = TimeSpan.FromHours(12);

    /// <summary>A game is on: kicked off and not final (a delayed start counts; a postponed game doesn't).</summary>
    public static bool IsLive(Competition game, DateTimeOffset now) =>
        !GameHelpers.IsGameOver(game) && game.Date <= now
        && now <= game.Date + (GameHelpers.IsGameStarted(game) ? OverlongGameLimit : LiveGameDuration);
}
