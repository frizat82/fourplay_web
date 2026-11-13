using FourPlayWebApp.Server.Models.Data;
using FourPlayWebApp.Shared.Models;
using FourPlayWebApp.Shared.Models.Enum;

namespace FourPlayWebApp.Server.Jobs;

// What one capture learned about a slate's still-scheduled games: the teams ranked right now, and
// the teams ESPN marks unranked — whose rank an earlier capture may have stored before they dropped
// out. Callers upsert Ranked (AddRankingsAsync) and clear Unranked (RemoveRankingsAsync).
internal sealed record CfbRankingCapture(List<CfbRanking> Ranked, List<CfbUnrankedSighting> Unranked);

// Shared by CfbSpreadJob (ranking capture riding along with its odds fetch, at spread-lock time)
// and CfbRankingCaptureJob (earlier capture, as soon as a week's schedule is known) — one
// implementation of "which CfbRanking rows does this batch of ESPN events produce for this slate."
internal static class CfbRankingExtractor {
    private const int EspnUnranked = 99;

    // Only still-scheduled games count: rank at kickoff is what applied to a game, so once it has
    // started a newer poll must neither overwrite nor clear what was captured for it.
    public static CfbRankingCapture ExtractFrom(IEnumerable<Event> events, CfbSeasonWeekConfig slate) {
        var capture = new CfbRankingCapture([], []);

        foreach (var evt in events) {
            var comp = evt.Competitions.FirstOrDefault();
            if (comp is null || comp.Status.Type.Name != TypeName.StatusScheduled) continue;

            foreach (var c in comp.Competitors) {
                if (CfbSlateHelpers.RankOf(c) is { } rank) {
                    capture.Ranked.Add(new CfbRanking {
                        Season           = slate.Season,
                        WeekNumber       = slate.SlateNumber,
                        TeamAbbreviation = c.Team.Abbreviation,
                        CuratedRank      = rank,
                    });
                } else if (c.CuratedRank?.Current == EspnUnranked) {
                    // Only ESPN's explicit "unranked" clears a stored rank: a rank that's missing or
                    // malformed in the response says nothing, so it's left alone.
                    capture.Unranked.Add(new CfbUnrankedSighting(slate.Season, slate.SlateNumber, c.Team.Abbreviation));
                }
            }
        }

        // A real week always has ranked teams playing. A response with none is a bad payload (or a
        // poll mid-reset), not 25 teams dropping out at once — clear nothing on its word.
        if (capture.Ranked.Count == 0) {
            capture.Unranked.Clear();
            return capture;
        }

        // One rank per team-week, and a team ESPN shows ranked anywhere this week is not cleared —
        // a team plays once per slate (#218), but a duplicated ESPN event must not undo a rank.
        var rankedTeams = capture.Ranked.Select(r => r.TeamAbbreviation).ToHashSet();
        return new CfbRankingCapture(
            capture.Ranked.DistinctBy(r => r.TeamAbbreviation).ToList(),
            capture.Unranked.Where(u => !rankedTeams.Contains(u.TeamAbbreviation)).Distinct().ToList());
    }
}
