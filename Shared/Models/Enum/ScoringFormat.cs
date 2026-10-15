using System.Text.Json.Serialization;

namespace FourPlayWebApp.Shared.Models.Enum;

/// <summary>
/// A week's scoring tier (frizat-b4g) — the same set for NFL and CFB. Each maps to exactly one pick
/// count, enforced on both control tables against RequiredPicks: Standard 4, Divisional 3,
/// Conference 2, Final 1, None 0 (a week that isn't bet on). Stored as its name.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ScoringFormat {
    Standard,
    Divisional,
    Conference,
    Final,
    None,
}
