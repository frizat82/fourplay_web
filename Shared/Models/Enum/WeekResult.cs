namespace FourPlayWebApp.Shared.Models.Enum;

public enum WeekResult
{
    Won,
    Lost,
    MissingPicks,
    MissingGameResults,
    // This week is before the league's configured StartWeek (frizat-o3x) — no picks were ever
    // required, so it's neither a win, a loss, nor pending; excluded entirely from settlement
    // (LeaderboardSettlementHelper must never bucket this into winners/losers or grow the pot).
    Excluded
}
