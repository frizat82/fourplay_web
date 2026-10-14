using System.ComponentModel.DataAnnotations.Schema;

namespace FourPlayWebApp.Server.Models.Data;

public class CfbSlates {
    public int Id { get; set; }
    public int Season { get; set; }
    public int SlateNumber { get; set; }            // 1-4 within a season
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
    public DateTimeOffset? FirstGameUtc { get; set; } // earliest kickoff, drives pick lock time
    public int? EspnWeekNumber { get; set; }          // actual ESPN week for API queries (decoupled from SlateNumber)
    public string? ScoringFormat { get; set; }        // "Standard" | "NFLDivisional" | "NFLConference" | "NFLSuperBowl"
    public DateTimeOffset DateCreated { get; set; } = DateTimeOffset.UtcNow;
    // The slate's CfbSeasonWeekConfigs row's name and rules (frizat-d47) — not stored here:
    // CfbRepository fills them from the control table on every slate read, so an edited row is
    // never shadowed by a copy frozen at seeding time.
    [NotMapped] public string Label { get; set; } = string.Empty;
    [NotMapped] public bool IsPostSeason { get; set; }
    [NotMapped] public int RequiredPicks { get; set; }
}
