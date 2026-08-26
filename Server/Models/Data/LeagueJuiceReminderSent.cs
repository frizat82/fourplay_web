namespace FourPlayWebApp.Server.Models.Data;

// frizat-ugs: tracks that LeagueJuiceReminderJob already emailed a league's owner for a given
// season. Neither Quartz's own trigger state (a completed one-time trigger looks identical to
// "never scheduled" once Quartz drops it — this app's Quartz job store is in-memory besides, so
// nothing survives a restart) nor "Juice is configured" (sending the reminder doesn't configure
// anything) is a valid "already reminded" signal — this small marker is the real one.
public class LeagueJuiceReminderSent {
    public int Id { get; set; }
    public int LeagueId { get; set; }
    public int Season { get; set; }
    public DateTimeOffset SentAt { get; set; } = DateTimeOffset.UtcNow;
}
