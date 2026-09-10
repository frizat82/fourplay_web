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

            foreach (var user in leagueUsers) {
                var userModel = new LeaderboardModel {
                    User = user.User,
                    WeekResults = new LeaderboardWeekResults[slates.Count],
                };

                for (int i = 0; i < slates.Count; i++) {
                    var slate = slates[i];

                    // frizat-o3x: a slate before the league's configured StartWeek needs no
                    // spread/score/pick fetch at all — checked here, before any DB call, not
                    // inside EvaluateSlate, so an excluded slate costs zero round trips per user
                    // instead of three wasted ones.
                    if (GameHelpers.IsWeekExcludedFromSeason(slate.SlateNumber, juiceMapping.StartWeek)) {
                        userModel.WeekResults[i] = new LeaderboardWeekResults { Week = slate.SlateNumber, WeekResult = WeekResult.Excluded };
                        continue;
                    }

                    // GetSpreadsForSlateAsync now returns the full FBS slate, not just league-eligible
                    // games (frizat-9m0) — scoring/MissingPicks must only ever consider eligible ones.
                    var spreads = (await cfbRepository.GetSpreadsForSlateAsync(slate.Id)).WhereLeagueEligible().ToList();
                    var scores = (await cfbRepository.GetScoresForSlateAsync(slate.Id)).ToList();
                    var picks = (await cfbPicksRepository.GetUserPicksAsync(leagueId, slate.Id, user.UserId)).ToList();
                    var juice = JuiceForSlate(slate.SlateNumber, juiceMapping);

                    userModel.WeekResults[i] = EvaluateSlate(slate.SlateNumber, spreads, scores, picks, juice, now);
                }

                leaderboard.Add(userModel);
            }

            leaderboard = CalculateTotals(leaderboard, juiceMapping, slates.Count);
        } catch (Exception ex) {
            logger.LogError(ex, "Error building CFB leaderboard for league {LeagueId}", leagueId);
        }

        return leaderboard;
    }

    // internal so CfbLeaderboardServiceTests can verify the per-slate tease amounts directly.
    internal static double JuiceForSlate(int slateNumber, LeagueJuiceMapping juice) => slateNumber switch {
        <= 14 => juice.Juice,
        <= 16 => juice.JuiceDivisional,  // quarterfinals (slates 15–16)
        _ => juice.JuiceConference,       // slate 17 Semifinals + slate 18 Championship
    };

    private LeaderboardWeekResults EvaluateSlate(int slateNumber, List<CfbSpreads> spreads, List<CfbScores> scores,
        List<CfbPicks> picks, double juice, DateTimeOffset now) {
        var result = new LeaderboardWeekResults { Week = slateNumber };

        // A user can submit or change a pick for any individual game right up until that game's own
        // kickoff (CfbPicksController.StartedTeams uses the identical GameTime <= now check) — so an
        // incomplete pick set is only a genuine, terminal loss once every eligible game in this slate
        // has already started. Until then it's no different from "not decided yet", so it reuses the
        // exact MissingGameResults state (frizat-tf1: a real user was shown as losing a week before
        // any of that week's games had even kicked off).
        var allGamesStarted = GameHelpers.AllGamesStarted(spreads.Select(s => s.GameTime), now);
        var incompletePicksResult = allGamesStarted ? WeekResult.MissingPicks : WeekResult.MissingGameResults;

        if (picks.Count == 0 && spreads.Count > 0) {
            result.WeekResult = incompletePicksResult;
            return result;
        }

        var allWon = picks.All(pick => {
            try {
                return DidPickWin(pick, spreads, scores, juice);
            } catch (Exception ex) {
                logger.LogError(ex, "Error evaluating CFB pick {@Pick}", pick);
                return false;
            }
        });

        if (!allWon) {
            result.WeekResult = WeekResult.Lost;
        } else if (picks.Count < GameHelpers.GetCfbRequiredPicks(slateNumber)) {
            result.WeekResult = incompletePicksResult;
        } else if (picks.Any(pick => !scores.Any(s => s.HomeTeam == pick.Team || s.AwayTeam == pick.Team))) {
            result.WeekResult = WeekResult.MissingGameResults;
        } else {
            result.WeekResult = WeekResult.Won;
        }

        return result;
    }

    private static bool DidPickWin(CfbPicks pick, List<CfbSpreads> spreads, List<CfbScores> scores, double juice) {
        var spread = spreads.FirstOrDefault(s => s.HomeTeam == pick.Team || s.AwayTeam == pick.Team);
        if (spread is null) return true;

        var score = scores.FirstOrDefault(s => s.HomeTeam == pick.Team || s.AwayTeam == pick.Team);
        if (score is null) return true; // game not yet scored

        var isHome = spread.HomeTeam == pick.Team;
        var teamScore = isHome ? score.HomeTeamScore : score.AwayTeamScore;
        var otherScore = isHome ? score.AwayTeamScore : score.HomeTeamScore;
        var rawSpread = isHome ? spread.HomeTeamSpread : spread.AwayTeamSpread;

        return pick.PickType switch {
            PickType.Spread => teamScore + rawSpread + juice - otherScore > 0,
            PickType.Over => teamScore + otherScore > spread.OverUnder - juice,
            PickType.Under => teamScore + otherScore < spread.OverUnder + juice,
            _ => false,
        };
    }

    private static List<LeaderboardModel> CalculateTotals(List<LeaderboardModel> leaderboard,
        LeagueJuiceMapping juiceMapping, int slateCount) =>
        LeaderboardSettlementHelper.SettleWeeks(leaderboard, juiceMapping.WeeklyCost, slateCount, juiceMapping.StartWeek);
}
