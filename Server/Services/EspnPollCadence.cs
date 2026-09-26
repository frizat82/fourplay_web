namespace FourPlayWebApp.Server.Services;

/// <summary>
/// Shared ESPN scoreboard poll cadence for EspnCacheService (NFL) and CfbCacheService (CFB) —
/// frizat-ucv. One shared value each, not two independently-tuned copies (CLAUDE.md's NFL/CFB
/// sharing rule), since there's no genuine sport-specific reason for these to ever diverge.
///
/// ESPN's real live-game duration varies (OT, delays) — 4h is a generous upper bound so the fast
/// poll doesn't drop out mid-game (a game that's gone final stops counting as live immediately).
/// Fast keeps the single shared poll (this cache fans out to every client, not one poll per
/// viewer) near-live during games; outside games ScorePollSchedule sleeps until the next known
/// kickoff instead. Slow is the retry cadence when nothing is known (startup, errors) and for a
/// game running past its window.
/// </summary>
public static class EspnPollCadence
{
    public static readonly TimeSpan LiveGameDuration = TimeSpan.FromHours(4);
    public static readonly TimeSpan FastPollInterval = TimeSpan.FromSeconds(30);
    public static readonly TimeSpan SlowPollInterval = TimeSpan.FromMinutes(5);
    // How long after kickoff a finished game is still re-checked for ESPN's post-final score
    // corrections — by both the poll schedule and EspnDayCache, so the two layers agree.
    public static readonly TimeSpan RecentlyFinishedWindow = TimeSpan.FromHours(12);
}
