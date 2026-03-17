namespace FourPlayWebApp.Shared.Models;

public class JobStatusResponse {
    // initialize strings to avoid non-nullable warnings
    public string JobName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTimeOffset? NextRun { get; set; }
    public DateTimeOffset? LastRun { get; set; }

    // Observer info (nullable so existing clients remain compatible)
    public DateTimeOffset? LastStartedUtc { get; set; }
    public DateTimeOffset? LastSucceededUtc { get; set; }
    public DateTimeOffset? LastFailedUtc { get; set; }
    public string? LastMessage { get; set; }
    public int RunCount { get; set; }
}
