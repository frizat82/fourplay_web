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
    public static readonly TimeSpan LiveGameDuration = TimeSpan.FromHours(4);
    public static readonly TimeSpan OverlongGameLimit = TimeSpan.FromHours(12);

    /// <summary>
    /// A game is on: past its kickoff time and not final. One that has started stays live until
    /// final (delays, OT), capped at <see cref="OverlongGameLimit"/> so a status stuck "in progress"
    /// at ESPN can't keep the pollers fast forever. One not started yet is a delayed start for up to
    /// <see cref="LiveGameDuration"/>; after that it's postponed/canceled (ESPN keeps those "scheduled").
    /// </summary>
    public static bool IsLive(Competition game, DateTimeOffset now) =>
        !GameHelpers.IsGameOver(game) && game.Date <= now
        && now <= game.Date + (GameHelpers.IsGameStarted(game) ? OverlongGameLimit : LiveGameDuration);

    /// <summary>A game that hasn't reached its kickoff time yet.</summary>
    public static bool IsUpcoming(Competition game, DateTimeOffset now) => !GameHelpers.IsGameOver(game) && game.Date > now;
}
