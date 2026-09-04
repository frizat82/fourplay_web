namespace FourPlayWebApp.Server.Models.Data;

// One row per (Season, EspnWeekNumber, TeamAbbreviation) — AP Top 25 rank does not change once
// captured for a given week, so later captures (CfbRankingCaptureJob, then CfbSpreadJob riding
// along with its odds fetch) upsert this row rather than appending a new one. Only genuinely
// ranked teams (1-25) get a row at all; ESPN's 99 "unranked" sentinel is filtered out before it
// ever reaches this table (see CfbRankingExtractor / CfbSlateHelpers.RankOf).
public class CfbRanking {
    public int Id { get; set; }
    public int Season { get; set; }
    public int EspnWeekNumber { get; set; }
    public int EspnEventId { get; set; }
    public string TeamAbbreviation { get; set; } = string.Empty;
    public int CuratedRank { get; set; }
    public DateTimeOffset CapturedAtUtc { get; set; } = DateTimeOffset.UtcNow;
}
