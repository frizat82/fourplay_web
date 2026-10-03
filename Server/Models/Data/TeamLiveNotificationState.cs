using FourPlayWebApp.Shared.Models.Enum;

namespace FourPlayWebApp.Server.Models.Data;

// Restart-survivable baseline/dedup state for the AGGREGATED "others" live notification channel —
// one row per (league, team, pick type, period), independent of PickLiveNotificationState's
// per-pick "mine" tracking. Every league member who made the SAME bet (same team, same PickType —
// an Over and a Spread pick anchored on the same team are different bets with different win
// conditions, so they're tracked separately) computes an identical cover state each tick, so
// "others" is deduped once per bet, not once per picker (frizat-cov).
public class TeamLiveNotificationState {
    public int Id { get; set; }
    public LeagueType Sport { get; set; }
    public int LeagueId { get; set; }
    public string Team { get; set; } = string.Empty;
    public PickType PickType { get; set; }
    // The FK-scoping period value (NflSeasonWeekConfig.WeekId / CfbSlates row id) — matches
    // PickLiveNotificationState's implicit per-pick scoping (a pick row belongs to one period).
    public int Period { get; set; }
    public bool? LastNotifiedCovering { get; set; }
    public DateTimeOffset? LastNotifiedAt { get; set; }
    public DateTimeOffset? FinalNotifiedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}
