namespace FourPlayWebApp.Server.Models.Data;

// Append-only audit trail of AP Top 25 rank at the moment CfbSpreadJob fetched a week's scoreboard —
// written for every competitor, ranked or not, so the full weekly rank picture is preserved even
// for teams that never became league-eligible (frizat-9m0). ESPN uses 99 for unranked.
public class CfbRanking {
    public int Id { get; set; }
    public int Season { get; set; }
    public int EspnWeekNumber { get; set; }
    public int EspnEventId { get; set; }
    public string TeamAbbreviation { get; set; } = string.Empty;
    public int CuratedRank { get; set; }
    public DateTime CapturedAtUtc { get; set; } = DateTime.UtcNow;
}
