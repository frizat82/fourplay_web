using FourPlayWebApp.Shared.Models.Enum;
using System.Text.Json.Serialization;

namespace FourPlayWebApp.Shared.Models;

public class LeaderboardWeekResults {
    public int Week { get; set; }
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public WeekResult WeekResult { get; set; }
    public long Score { get; set; }
}
