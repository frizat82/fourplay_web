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

            // Every query here is per season, never per member or per slate — three queries total,
            // the same shape as the NFL leaderboard (a 20-member league at slate 18 used to be 1,080
            // sequential round trips). Each repository call has its own DbContext, so they run together.
            var picksTask = cfbPicksRepository.GetLeaguePicksForSeasonAsync(leagueId, season);
            var spreadsTask = cfbRepository.GetLeagueEligibleSpreadsForSeasonAsync(season);
            var scoresTask = cfbRepository.GetScoresForSeasonAsync(season);
            // Scoring/MissingPicks only ever consider league-eligible games (frizat-9m0).
            var spreadsBySlate = (await spreadsTask).ToLookup(s => s.CfbSlateId);
            var scoresBySlate = (await scoresTask).ToLookup(s => s.CfbSlateId);
            var picksBySlateUser = (await picksTask).ToLookup(p => (p.CfbSlateId, p.UserId), p => new PickRow(p.Team, p.PickType));
            var slateIdByNumber = slates.ToDictionary(s => s.SlateNumber, s => s.Id);

            var periods = slates
                .Select(slate => new LeaderboardPeriod(slate.SlateNumber, spreadsBySlate[slate.Id].ToList<IOddsRow>(), scoresBySlate[slate.Id].ToList<IScoreRow>()))
                .ToList();

            leaderboard = LeaderboardEngine.Build(LeagueType.Cfb, leagueUsers, periods,
                (userId, slateNumber) => picksBySlateUser[(slateIdByNumber[slateNumber], userId)], juiceMapping, timeProvider.GetUtcNow(),
                (pick, slateNumber, ex) => logger.LogError(ex, "Error scoring CFB pick {@Pick} slate {Slate}", pick, slateNumber));
        } catch (Exception ex) {
            logger.LogError(ex, "Error building CFB leaderboard for league {LeagueId}", leagueId);
        }

        return leaderboard;
    }
}
