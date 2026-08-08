using FourPlayWebApp.Server.Services.Interfaces;
using FourPlayWebApp.Server.Services.Repositories.Interfaces;
using FourPlayWebApp.Shared.Models;
using FourPlayWebApp.Shared.Models.Data;
using FourPlayWebApp.Shared.Models.Enum;
using Quartz;
using Serilog;

namespace FourPlayWebApp.Server.Jobs;

[DisallowConcurrentExecution]
public class CfbSpreadJob(ICfbLiveScoreFetcher fetcher, IEspnCoreOddsService oddsService, ICfbRepository repo) : IJob {
    private const int Season = 2026;

    public async Task Execute(IJobExecutionContext context) {
        Log.Information("CfbSpreadJob: fetching CFB spreads at {Time}", DateTime.UtcNow);

        var slates = (await repo.GetSlatesForSeasonAsync(Season)).ToList();
        if (slates.Count == 0) {
            Log.Warning("CfbSpreadJob: no slates found for season {Season} — run CfbSlateSeederJob first", Season);
            return;
        }

        var spreads = new List<CfbSpreads>();
        var rankings = new List<Models.Data.CfbRanking>();

        foreach (var slate in slates) {
            var scoreboard = await fetcher.FetchForSlateAsync(slate);
            if (scoreboard?.Events is null) continue;
            await ProcessEventsForSpreads(spreads, rankings, slate, scoreboard.Events);
        }

        if (spreads.Count > 0) {
            await repo.AddCfbSpreadsAsync(spreads);
            Log.Information("CfbSpreadJob: saved {Count} CFB spreads", spreads.Count);
        }
        if (rankings.Count > 0) {
            await repo.AddRankingsAsync(rankings);
            Log.Information("CfbSpreadJob: saved {Count} CFB rankings", rankings.Count);
        }
        Log.Information("CfbSpreadJob: complete at {Time}", DateTime.UtcNow);
    }

    private async Task ProcessEventsForSpreads(
        List<CfbSpreads> spreads,
        List<Models.Data.CfbRanking> rankings,
        FourPlayWebApp.Server.Models.Data.CfbSlates slate,
        IEnumerable<FourPlayWebApp.Shared.Models.Event> events) {
        var isCfp = CfbSlateHelpers.IsCfpSlate(slate.ScoringFormat);

        foreach (var evt in events) {
            var comp = evt.Competitions.FirstOrDefault();
            if (comp is null || comp.Status.Type.Name != TypeName.StatusScheduled) continue;

            var eventId = int.Parse(evt.Id);
            var home = comp.Competitors.FirstOrDefault(c => c.HomeAway == HomeAway.Home)?.Team.Abbreviation ?? "";
            var away = comp.Competitors.FirstOrDefault(c => c.HomeAway == HomeAway.Away)?.Team.Abbreviation ?? "";

            // Rank capture and eligibility are orthogonal to odds availability — record them
            // for every scheduled game so the audit trail isn't gated on a successful odds fetch.
            rankings.AddRange(comp.Competitors
                .Where(c => c.CuratedRank is not null)
                .Select(c => new Models.Data.CfbRanking {
                    Season           = slate.Season,
                    EspnWeekNumber   = slate.EspnWeekNumber ?? 0,
                    EspnEventId      = eventId,
                    TeamAbbreviation = c.Team.Abbreviation,
                    CuratedRank      = c.CuratedRank!.Current,
                }));

            var isEligible = isCfp
                || (CfbSlateHelpers.HasRankedTeam(comp.Competitors) && !CfbSlateHelpers.IsMidweekGame(comp.Date));

            try {
                var odds = await oddsService.GetCfbEventsWithOddsAsync(eventId, (int)EspnOddsProviders.DraftKings);
                if (odds is null) {
                    var allOdds = await oddsService.GetCfbEventsWithOddsAsync(eventId);
                    if (allOdds is null || allOdds.Count == 0) {
                        Log.Warning("CfbSpreadJob: no odds for event {EventId} ({Home} vs {Away})", eventId, home, away);
                        continue;
                    }
                    odds = allOdds.Items.First();
                }

                var homeSpread = odds.HomeTeamOdds.Current.PointSpread.American.Replace("+", "");
                var awaySpread = odds.AwayTeamOdds.Current.PointSpread.American.Replace("+", "");
                if (!double.TryParse(homeSpread, out var parsedHome)) continue;
                if (!double.TryParse(awaySpread, out var parsedAway)) continue;

                spreads.Add(new CfbSpreads {
                    CfbSlateId    = slate.Id,
                    EspnEventId   = eventId,
                    HomeTeam      = home,
                    AwayTeam      = away,
                    HomeTeamSpread = parsedHome,
                    AwayTeamSpread = parsedAway,
                    OverUnder     = odds.OverUnder,
                    GameTime      = comp.Date,
                    IsLeagueEligible = isEligible,
                });
            } catch (Exception ex) {
                Log.Error(ex, "CfbSpreadJob: error fetching odds for event {EventId}", eventId);
            }
        }
    }
}
