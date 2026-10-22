using FourPlayWebApp.Shared.Models.Enum;

namespace FourPlayWebApp.Server.Models.Data;

// Records that a sport's week already had its "spreads are up" push, so a re-run of that week's
// spread job (a retry, an admin re-run) never sends it twice. One row per sport/season/week.
public class SpreadsPostedNotificationSent {
    public int Id { get; set; }
    public LeagueType Sport { get; set; }
    public int Season { get; set; }
    public int Week { get; set; }
    public DateTimeOffset SentAt { get; set; } = DateTimeOffset.UtcNow;
}
