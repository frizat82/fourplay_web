namespace FourPlayWebApp.Server.Models.Data;

// Tracks that WeekResultNotificationJob already sent a push for this user's week-result in this
// league/season — mirrors LeagueJuiceReminderSent's dedup role. Needed because the job re-checks
// every league on a recurring cron (so it can catch a week that just became decided), and without
// this, a user who already got "you went 4/4" would get it again on every subsequent run.
public class WeekResultNotificationSent {
    public int Id { get; set; }
    public string UserId { get; set; } = string.Empty;
    public int LeagueId { get; set; }
    public int Season { get; set; }
    public int Week { get; set; }
    public DateTimeOffset SentAt { get; set; } = DateTimeOffset.UtcNow;
}
