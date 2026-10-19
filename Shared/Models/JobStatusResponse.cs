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
    // The leagues this job affects, by name: a per-league job's one league, or every league of a
    // sport-wide job's sport (scores, spreads, rankings). Empty for system jobs. Resolved
    // server-side so the frontend never pattern-matches ids back out of Description's free text.
    public List<string> LeagueNames { get; set; } = [];
}
