namespace FourPlayWebApp.Server.Models.Data;

public class CfbSlates {
    public int Id { get; set; }
    public int Season { get; set; }
    public int SlateNumber { get; set; }            // 1-4 within a season
    public string Label { get; set; } = string.Empty; // "CFP First Round", "Quarterfinals", etc
    public string SlateType { get; set; } = string.Empty; // "FirstRound" | "Quarterfinal" | "Semifinal" | "Championship"
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
    public DateTimeOffset? FirstGameUtc { get; set; } // earliest kickoff, drives pick lock time
    public int? EspnWeekNumber { get; set; }          // actual ESPN week for API queries (decoupled from SlateNumber)
    public string? ScoringFormat { get; set; }        // "Standard" | "NFLDivisional" | "NFLConference" | "NFLSuperBowl"
    public DateTimeOffset DateCreated { get; set; } = DateTimeOffset.UtcNow;
}
