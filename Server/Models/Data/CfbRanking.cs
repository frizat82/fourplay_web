namespace FourPlayWebApp.Server.Models.Data;

// One row per (Season, WeekNumber, TeamAbbreviation), kept current until that team's game kicks
// off: later captures (CfbRankingCaptureJob, then CfbSpreadJob riding along with its odds fetch)
// upsert this row, and remove it if the team has since dropped out of the poll. Only genuinely
// ranked teams (1-25) get a row at all; ESPN's 99 "unranked" sentinel is filtered out before it
// ever reaches this table (see CfbRankingExtractor / CfbSlateHelpers.RankOf).
public class CfbRanking {
    public int Id { get; set; }
    public int Season { get; set; }
    // Our CFB week (the slate number) — ranks are read off the games fetched for that week's
    // dates, so they tie out by date, never by ESPN's own week numbering (frizat-b4g).
    public int WeekNumber { get; set; }
    public int EspnEventId { get; set; }
    public string TeamAbbreviation { get; set; } = string.Empty;
    public int CuratedRank { get; set; }
    public DateTimeOffset CapturedAtUtc { get; set; } = DateTimeOffset.UtcNow;
}

// A team ESPN marks unranked in a still-scheduled game: the rank an earlier capture stored for
// that same game is stale (the team dropped out of the poll since) and gets removed.
public sealed record CfbUnrankedSighting(int Season, int WeekNumber, string TeamAbbreviation, int EspnEventId);
