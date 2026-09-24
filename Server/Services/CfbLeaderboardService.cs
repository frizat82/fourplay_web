using FourPlayWebApp.Server.Models.Data;
using FourPlayWebApp.Server.Services.Interfaces;
using FourPlayWebApp.Server.Services.Repositories.Interfaces;
using FourPlayWebApp.Shared.Helpers;
using FourPlayWebApp.Shared.Models;
using FourPlayWebApp.Shared.Models.Data;
using FourPlayWebApp.Shared.Models.Enum;
using ILogger = Microsoft.Extensions.Logging.ILogger;

namespace FourPlayWebApp.Server.Services;

public class CfbLeaderboardService(
    ILogger<CfbLeaderboardService> logger,
    ILeagueRepository leagueRepository,
    ICfbRepository cfbRepository,
    ICfbPicksRepository cfbPicksRepository,
    ICfbCurrentSlateService currentSlateService,
    TimeProvider timeProvider)
    : ICfbLeaderboardService {

    public async Task<List<LeaderboardModel>> BuildLeaderboard(int leagueId, int season) {
        var leaderboard = new List<LeaderboardModel>();
        if (leagueId == 0) return leaderboard;

        try {
            // Four independent lookups (no data dependency between them, each on its own
            // DbContext via the repository pattern) — kick them all off before awaiting instead
            // of paying for four sequential round-trips.
            var leagueUsersTask = leagueRepository.GetLeagueUserMappingsAsync(leagueId);
            var juiceMappingTask = leagueRepository.GetLeagueJuiceMappingAsync(leagueId, season);
            var slatesTask = cfbRepository.GetSlatesForSeasonAsync(season);
            var currentSlateTask = currentSlateService.GetCurrentSlateAsync();

            var leagueUsers = await leagueUsersTask;
            var juiceMapping = await juiceMappingTask;
            var slates = (await slatesTask).OrderBy(s => s.SlateNumber).ToList();

            // CfbSlates is fully seeded for the whole season up front (CfbSlateSeederJob), unlike
            // NFL's NflScores rows, which only exist once a game is FINAL — so NFL's leaderboard
            // naturally stops at "now" for free, while CFB needs this explicit clamp to avoid
            // showing every future slate as a missed week. Uses the same shared "what's current"
            // resolver NflCurrentWeekService/CfbCurrentSlateService both already go through
            // (SeasonWindowResolver), not a CFB-only reimplementation.
            var currentSlate = await currentSlateTask;
            if (currentSlate is not null) {
                if (currentSlate.Season == season)
                    slates = slates.Where(s => s.SlateNumber <= currentSlate.SlateNumber).ToList();
                else if (season > currentSlate.Season)
                    slates = []; // that season hasn't started yet
                // season < currentSlate.Season: a fully completed past season — show it all.
            }

            if (juiceMapping is null || leagueUsers.Count == 0 || slates.Count == 0)
                return leaderboard;

            // Captured once for the whole run rather than re-queried per user/slate — otherwise the
            // wall clock ticking past a kickoff boundary mid-run could give different users a
            // different MissingPicks/MissingGameResults verdict for the identical slate.
            var now = timeProvider.GetUtcNow();

            // Every query here is per slate or per season, never per member: a slate's spreads and
            // scores are identical for everyone, and all members' picks come from one season-wide
            // query (a 20-member league at slate 18 was 1,080 sequential round trips before).
            // frizat-o3x: slates before the league's StartWeek are never fetched at all.
            var picksTask = cfbPicksRepository.GetLeaguePicksForSeasonAsync(leagueId, season);
            var slateData = new Dictionary<int, (List<CfbSpreads> Spreads, List<CfbScores> Scores)>();
            foreach (var slate in slates) {
                if (GameHelpers.IsWeekExcludedFromSeason(slate.SlateNumber, juiceMapping.StartWeek)) continue;
                // Own DbContext per repository call, so the pair can run concurrently.
                var spreadsTask = cfbRepository.GetSpreadsForSlateAsync(slate.Id);
                var scoresTask = cfbRepository.GetScoresForSlateAsync(slate.Id);
                // GetSpreadsForSlateAsync returns the full FBS slate, not just league-eligible games
                // (frizat-9m0) — scoring/MissingPicks must only ever consider eligible ones.
                slateData[slate.Id] = ((await spreadsTask).WhereLeagueEligible().ToList(), (await scoresTask).ToList());
            }

            var picksBySlateUser = (await picksTask).ToLookup(p => (p.CfbSlateId, p.UserId));

            foreach (var user in leagueUsers) {
                var userModel = new LeaderboardModel {
                    User = user.User,
                    WeekResults = new LeaderboardWeekResults[slates.Count],
                };

                for (int i = 0; i < slates.Count; i++) {
                    var slate = slates[i];
                    if (!slateData.TryGetValue(slate.Id, out var data)) {
                        userModel.WeekResults[i] = new LeaderboardWeekResults { Week = slate.SlateNumber, WeekResult = WeekResult.Excluded };
                        continue;
                    }

                    var picks = picksBySlateUser[(slate.Id, user.UserId)].ToList();
                    var juice = JuiceTiers.For(LeagueType.Cfb, slate.SlateNumber, juiceMapping);
                    userModel.WeekResults[i] = EvaluateSlate(slate.SlateNumber, data.Spreads, data.Scores, picks, juice, now);
                }

                leaderboard.Add(userModel);
            }

            leaderboard = CalculateTotals(leaderboard, juiceMapping, slates.Count);
        } catch (Exception ex) {
            logger.LogError(ex, "Error building CFB leaderboard for league {LeagueId}", leagueId);
        }

        return leaderboard;
    }

    // Everything sport-specific (which spreads/scores/picks, which tease) is resolved by the caller;
    // how the slate resolves is WeekOutcome — the same rules the NFL leaderboard uses.
    private static LeaderboardWeekResults EvaluateSlate(int slateNumber, List<CfbSpreads> spreads, List<CfbScores> scores,
        List<CfbPicks> picks, double juice, DateTimeOffset now) => new() {
        Week = slateNumber,
        WeekResult = WeekOutcome.Evaluate(
            picks.Select(p => new PickRow(p.Team, p.PickType)).ToList(),
            scores,
            new SpreadCalculator(spreads, juice),
            GameHelpers.GetCfbRequiredPicks(slateNumber),
            GameHelpers.AllGamesStarted(spreads.Select(s => s.GameTime), now)),
    };

    private static List<LeaderboardModel> CalculateTotals(List<LeaderboardModel> leaderboard,
        LeagueJuiceMapping juiceMapping, int slateCount) =>
        LeaderboardSettlementHelper.SettleWeeks(leaderboard, juiceMapping.WeeklyCost, slateCount, juiceMapping.StartWeek);
}
