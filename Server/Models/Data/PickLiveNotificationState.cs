using FourPlayWebApp.Shared.Models.Enum;

namespace FourPlayWebApp.Server.Models.Data;

// Restart-survivable baseline/dedup state for one pick's live cover-state notifications (frizat-tgk
// Phase 3). Must be DB-persisted, not an in-memory dict (unlike DiscordJobFailureNotifier's dedup) —
// a mid-Sunday Railway restart must not cause duplicate or missed live pushes.
public class PickLiveNotificationState {
    public int Id { get; set; }
    // PickId alone is ambiguous between NflPicks.Id and CfbPicks.Id.
    public LeagueType Sport { get; set; }
    public int PickId { get; set; }
    // Denormalized — avoids a re-join to find "who else is in this league" on every recompute.
    public int LeagueId { get; set; }
    // Null = no baseline captured yet (still inside the post-kickoff quiet window).
    public bool? LastNotifiedCovering { get; set; }
    public DateTimeOffset? LastNotifiedAt { get; set; }
    // Independent of LastNotifiedCovering/LastNotifiedAt — the one-time "pick is final" ping fires
    // regardless of recent in-game transition history.
    public DateTimeOffset? FinalNotifiedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}
