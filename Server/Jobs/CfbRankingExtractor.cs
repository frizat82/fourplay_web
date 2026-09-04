using FourPlayWebApp.Server.Models.Data;
using FourPlayWebApp.Shared.Models;
using FourPlayWebApp.Shared.Models.Enum;

namespace FourPlayWebApp.Server.Jobs;

// Shared by CfbSpreadJob (ranking capture riding along with its odds fetch, at spread-lock time)
// and CfbRankingCaptureJob (earlier capture, as soon as a week's schedule is known) — one
// implementation of "which CfbRanking rows does this batch of ESPN events produce for this slate."
internal static class CfbRankingExtractor {
    public static List<CfbRanking> ExtractFrom(IEnumerable<Event> events, CfbSlates slate) {
        var rankings = new List<CfbRanking>();

        foreach (var evt in events) {
            var comp = evt.Competitions.FirstOrDefault();
            if (comp is null || comp.Status.Type.Name != TypeName.StatusScheduled) continue;

            var eventId = int.Parse(evt.Id);
            rankings.AddRange(comp.Competitors
                .Where(c => CfbSlateHelpers.RankOf(c) is not null)
                .Select(c => new CfbRanking {
                    Season           = slate.Season,
                    EspnWeekNumber   = slate.EspnWeekNumber ?? 0,
                    EspnEventId      = eventId,
                    TeamAbbreviation = c.Team.Abbreviation,
                    CuratedRank      = c.CuratedRank!.Current,
                }));
        }

        return rankings;
    }
}
