using FourPlayWebApp.Shared.Models.Enum;
using System.Text.Json.Serialization;

namespace FourPlayWebApp.Shared.Models;

public class LeaderboardWeekResults {
    public int Week { get; set; }
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public WeekResult WeekResult { get; set; }
    public long Score { get; set; }
    /// <summary>Which team(s) the user lost on — populated only when WeekResult is Lost.</summary>
    public IReadOnlyList<string> LosingTeams { get; set; } = [];
    /// <summary>
    /// True if WeekResult = Lost came from a pick that failed to score (bad data), not a legitimate
    /// loss against the spread. A page render self-corrects once the data is fixed; a consumer that
    /// permanently records this result (e.g. a push notification) should not.
    /// </summary>
    public bool HadScoringError { get; set; }
}
