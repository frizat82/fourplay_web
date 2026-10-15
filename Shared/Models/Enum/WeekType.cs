using System.Text.Json.Serialization;

namespace FourPlayWebApp.Shared.Models.Enum;

/// <summary>
/// What a control-table week is (frizat-b4g) — the same set for NFL and CFB. A week's name lives
/// in its WeekLabel ("Wild Card Weekend", "CFP Semifinals"); whether it can be bet on lives in
/// InScopeIvLeague. Stored as its name.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum WeekType {
    RegularSeason,
    ConferenceChampionships,
    // NFL Wild Card through Super Bowl; CFB's College Football Playoff rounds. A CFB Playoff week's
    // games come from ESPN's CFP endpoint, kept to the week's dates.
    Playoff,
    // A week with nothing to play (e.g. the gap between CFP rounds).
    Dead,
}
