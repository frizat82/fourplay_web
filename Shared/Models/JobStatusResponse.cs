namespace FourPlayWebApp.Shared.Models;

public class JobStatusResponse {
    public string JobName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTimeOffset? NextRun { get; set; }
    public DateTimeOffset? LastSucceededUtc { get; set; }
    public DateTimeOffset? LastFailedUtc { get; set; }
    public string? LastMessage { get; set; }
    // Human-readable grouping for the admin Job Manager table (e.g. "NFL Spreads", "Juice") —
    // derived server-side from the job's CLR type, see JobCategoryClassifier.
    public string Category { get; set; } = "System";
    // True for the numerous per-league/per-week jobs TimedTriggerScheduler registers dynamically
    // (Juice Reminder/Lock, NFL/CFB Spreads) — false for the fixed set of scheduler/cron jobs
    // registered directly in Program.cs. Lets the admin UI hide the noisy set by default.
    public bool IsDynamic { get; set; }
    // Present only for Juice Reminder/Lock jobs (from the job's JobDataMap).
    public int? LeagueId { get; set; }
    // Which sport the job is for, "NFL" or "CFB" (the admin Job Manager's League column): a
    // sport-wide job's own sport (scores, spreads, rankings), or a per-league job's league's
    // sport. Null for system jobs, and for a per-league job whose league no longer exists.
    public string? Sport { get; set; }
}
