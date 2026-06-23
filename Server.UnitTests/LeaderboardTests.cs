using FourPlayWebApp.Server.Data;
using FourPlayWebApp.Server.Models.Data;
using FourPlayWebApp.Server.Models.Identity;
using FourPlayWebApp.Server.Services;
using FourPlayWebApp.Server.Services.Interfaces;
using FourPlayWebApp.Server.Services.Repositories;
using FourPlayWebApp.Server.Services.Repositories.Interfaces;
using FourPlayWebApp.Shared.Helpers;
using FourPlayWebApp.Shared.Models;
using FourPlayWebApp.Shared.Models.Data;
using FourPlayWebApp.Shared.Models.Enum;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Serilog;
using Xunit.Abstractions;

namespace FourPlayWebApp.Server.UnitTests;

public class LeaderboardServiceTests {

    public LeaderboardServiceTests(ITestOutputHelper output) {
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            // add the xunit test output sink to the serilog logger
            // https://github.com/trbenning/serilog-sinks-xunit#serilog-sinks-xunit
            .WriteTo.TestOutput(output)
            .CreateLogger();
    }

    /// <summary>
    /// Wraps an ISpreadCalculatorBuilder inside a fake IServiceScopeFactory so that
    /// LeaderboardService (which now resolves ISpreadCalculatorBuilder via DI scope)
    /// can be tested without a full DI container.
    /// </summary>
    private static IServiceScopeFactory BuildScopeFactory(ISpreadCalculatorBuilder spreadCalculatorBuilder)
    {
        var serviceProvider = Substitute.For<IServiceProvider>();
        serviceProvider.GetService(typeof(ISpreadCalculatorBuilder))
                       .Returns(spreadCalculatorBuilder);

        var scope = Substitute.For<IServiceScope>();
        scope.ServiceProvider.Returns(serviceProvider);

        var asyncScope = Substitute.For<IAsyncDisposable>();

        // AsyncServiceScope is a value type wrapping IServiceScope — we can't mock it directly,
        // so we create a real one from our fake IServiceScope.
        var scopeFactory = Substitute.For<IServiceScopeFactory>();
        scopeFactory.CreateScope().Returns(scope);
        // CreateAsyncScope() returns AsyncServiceScope (struct) backed by the same scope.
        scopeFactory.CreateScope().Returns(scope);

        return scopeFactory;
    }

    [AutoNSubData, Theory]
    public async Task BuildLeaderboard_ReturnsEmptyList_WhenLeagueIdIsZero(
        ISpreadCalculatorBuilder spreadCalculator, ILeagueRepository repository) {
        var memoryCache = new MemoryCache(new MemoryCacheOptions());
        var scopeFactory = BuildScopeFactory(spreadCalculator);

        var service = new LeaderboardService(new LoggerFactory().CreateLogger<LeaderboardService>(), scopeFactory, repository);

        var result = await service.BuildLeaderboard(0, 2024);

        Assert.Empty(result);
    }

    [Fact]
    public async Task CalculatePostSeasonPicks_ReturnsCorrectResults() {
        // Arrange
        var dbFactory = new DbContextFactoryStub();
        await dbFactory.PopulateUserTestData();
        await dbFactory.PopulateScoresTestDataAsync();
        var repository = new LeagueRepository(dbFactory);
        var spreadCalculatorBuilder =
            new SpreadCalculatorBuilder(repository, new MemoryCache(new MemoryCacheOptions()));
        var dbContext = await dbFactory.CreateDbContextAsync();
        var scores = dbContext.NflScores.ToList();
        // Generate regular season
        _ = await FakePicks(spreadCalculatorBuilder, dbContext, scores, 18);
        // Generate playoff picks and get the results
        var playoffResults = await FakePlayoffPicks(spreadCalculatorBuilder, dbContext, scores);
        // Create service and build leaderboard
        var scopeFactory = BuildScopeFactory(spreadCalculatorBuilder);
        var service = new LeaderboardService(new LoggerFactory().CreateLogger<LeaderboardService>(), scopeFactory, repository);
        var result = await service.BuildLeaderboard(1, 2024);

        // Assert: 3 playoff rounds (19 = Wild Card, 20 = Divisional, 21 = Conf. Championship)
        // Week 22 (Pro Bowl) is skipped; Super Bowl (Week 23) not seeded in test data
        Assert.True((playoffResults[0] ? WeekResult.Won : WeekResult.Lost) == result.First().WeekResults[18].WeekResult, "Week 19 Wild Card result mismatch");
        Assert.True((playoffResults[1] ? WeekResult.Won : WeekResult.Lost) == result.First().WeekResults[19].WeekResult, "Week 20 Divisional result mismatch");
        Assert.True((playoffResults[2] ? WeekResult.Won : WeekResult.Lost) == result.First().WeekResults[20].WeekResult, "Week 21 Conference result mismatch");
    }

    [Fact]
    public async Task CalculateRegularSeasonPicks_ReturnsWon_WhenAllPicksBeatSpread_FalseWhenNot_MissingWhenMissing() {
        var dbFactory = new DbContextFactoryStub();
        await dbFactory.PopulateUserTestData();
        await dbFactory.PopulateScoresTestDataAsync();
        var repository = new LeagueRepository(dbFactory);
        var spreadCalculatorBuilder =
            new SpreadCalculatorBuilder(repository, new MemoryCache(new MemoryCacheOptions()));
        var dbContext = await dbFactory.CreateDbContextAsync();
        var scores = dbContext.NflScores.ToList();
        var isWinner = await FakePicks(spreadCalculatorBuilder, dbContext, scores, 4);

        await dbContext.SaveChangesAsync();

        var scopeFactory = BuildScopeFactory(spreadCalculatorBuilder);
        var service = new LeaderboardService(new LoggerFactory().CreateLogger<LeaderboardService>(), scopeFactory, repository);

        var result = await service.BuildLeaderboard(1, 2024);

        Assert.Equal(isWinner[0] ? WeekResult.Won : WeekResult.Lost, result.First().WeekResults[0].WeekResult);
        Assert.Equal(isWinner[1] ? WeekResult.Won : WeekResult.Lost, result.First().WeekResults[1].WeekResult);
        Assert.Equal(isWinner[2] ? WeekResult.Won : WeekResult.Lost, result.First().WeekResults[2].WeekResult);
        Assert.Equal(isWinner[3] ? WeekResult.Won : WeekResult.Lost, result.First().WeekResults[3].WeekResult);
        Assert.Equal(WeekResult.MissingPicks, result.First().WeekResults[4].WeekResult);

    }

    [Fact]
    public async Task CalculateUserTotals_ReturnsCorrectTotals_WithMultipleWinnersAndLosers() {
        // Arrange
        var dbFactory = new DbContextFactoryStub();
        await dbFactory.PopulateUserTestData(5); // Create 5 users (4 winners, 1 loser per week)
        await dbFactory.PopulateScoresTestDataAsync(4); // 4 weeks of scores
        var repository = new LeagueRepository(dbFactory);
        var spreadCalculatorBuilder =
            new SpreadCalculatorBuilder(repository, new MemoryCache(new MemoryCacheOptions()));
        var dbContext = await dbFactory.CreateDbContextAsync();

        // Set the weekly cost to $10
        var league = dbContext.LeagueJuiceMapping.First();
        Assert.NotNull(league);
        league.WeeklyCost = 10;
        await dbContext.SaveChangesAsync();

        // Create leaderboard with 4 weeks of data, 5 users
        var leaderboard = new List<LeaderboardModel>();
        var users = dbContext.Users.Take(5).ToList();

        for (int userIndex = 0; userIndex < 5; userIndex++) {
            var user = users[userIndex];
            var userModel = new LeaderboardModel {
                User = user,
                WeekResults = new LeaderboardWeekResults[4]
            };

            // Setup 4 weeks of results
            for (int week = 1; week <= 4; week++) {
                var weekResult = new LeaderboardWeekResults {
                    Week = week,
                    // Last user is always a loser, others are winners
                    WeekResult = userIndex == 4 ? WeekResult.Lost : WeekResult.Won
                };

                userModel.WeekResults[week - 1] = weekResult;
            }

            leaderboard.Add(userModel);
        }

        // Act
        var scopeFactory = BuildScopeFactory(spreadCalculatorBuilder);
        var service = new LeaderboardService(new LoggerFactory().CreateLogger<LeaderboardService>(), scopeFactory, repository);
        var result = await service.CalculateUserTotals(leaderboard, league.Id, 2024, 4);

        // Assert
        // Expected calculations:
        // - 4 winners each week, 1 loser
        // - Weekly cost is $10
        // - Each winner gets $10 from the loser, so +$10 per winner
        // - Loser pays $10 to each winner, so -$40 per week

        // Check winners (first 4 users)
        for (int i = 0; i < 4; i++) {
            var user = result[i];

            // Each winner should have +$10 for each week
            for (int week = 1; week <= 4; week++) {
                Assert.Equal(10, user.WeekResults[week - 1].Score);
            }

            // Total should be $40 ($10 × 4 weeks)
            Assert.Equal(40, user.Total);
        }

        // Check loser (5th user)
        var loser = result[4];

        // Loser should have -$40 for each week (paying $10 to each of 4 winners)
        for (int week = 1; week <= 4; week++) {
            Assert.Equal(-40, loser.WeekResults[week - 1].Score);
        }

        // Total should be -$160 (-$40 × 4 weeks)
        Assert.Equal(-160, loser.Total);

        // Verify sum of all user totals is zero (closed system)
        Assert.Equal(0, result.Sum(u => u.Total));
    }

    [Fact]
    public async Task CalculateUserTotals_WithJuiceDoubling_WhenAllUsersWinOrLose()
    {
        // Arrange
        var dbFactory = new DbContextFactoryStub();
        await dbFactory.PopulateUserTestData(5); // Create 5 users
        await dbFactory.PopulateScoresTestDataAsync(4); // 4 weeks of scores
        var repository = new LeagueRepository(dbFactory);
        var spreadCalculatorBuilder = new SpreadCalculatorBuilder(repository, new MemoryCache(new MemoryCacheOptions()));
        var dbContext = await dbFactory.CreateDbContextAsync();

        // Set the weekly cost to $10
        var league = dbContext.LeagueJuiceMapping.First();
        Assert.NotNull(league);
        league.WeeklyCost = 10;
        await dbContext.SaveChangesAsync();

        // Create leaderboard with 4 weeks of data, 5 users
        var leaderboard = new List<LeaderboardModel>();
        var users = dbContext.Users.Take(5).ToList();

        for (int userIndex = 0; userIndex < 5; userIndex++)
        {
            var user = users[userIndex];
            var userModel = new LeaderboardModel
            {
                User = user,
                WeekResults = new LeaderboardWeekResults[4]
            };

            // Setup 4 weeks of results with specific patterns:
            // Week 1: 4 winners, 1 loser (normal scenario)
            // Week 2: All users lose (juice doubles to $20)
            // Week 3: All users win (juice doubles to $30)
            // Week 4: 3 winners, 2 losers (with doubled juice from previous weeks)

            for (int week = 1; week <= 4; week++)
            {
                var weekResult = new LeaderboardWeekResults { Week = week };

                weekResult.WeekResult = week switch {
                    1 =>
                        // Week 1: 4 winners, 1 loser
                        userIndex < 4 ? WeekResult.Won : WeekResult.Lost,
                    2 =>
                        // Week 2: All users lose
                        WeekResult.Lost,
                    3 =>
                        // Week 3: All users win
                        WeekResult.Won,
                    4 =>
                        // Week 4: 3 winners, 2 losers
                        userIndex < 3 ? WeekResult.Won : WeekResult.Lost,
                    _ => weekResult.WeekResult
                };

                userModel.WeekResults[week - 1] = weekResult;
            }

            leaderboard.Add(userModel);
        }

        // Act
        var scopeFactory = BuildScopeFactory(spreadCalculatorBuilder);
        var service = new LeaderboardService(new LoggerFactory().CreateLogger<LeaderboardService>(), scopeFactory, repository);
        var result = await service.CalculateUserTotals(leaderboard, league.Id, 2024, 4);

        // Assert
        // Expected calculations:
        // - Week 1: Base cost $10, 4 winners, 1 loser
        //   * Winners: +$10 each
        //   * Loser: -$40
        // - Week 2: All users lose, juice doubles to $20, everyone scores $0
        // - Week 3: All users win, juice doubles to $30, everyone scores $0
        // - Week 4: 3 winners, 2 losers with juice of $30
        //   * Winners: +$60 each ($30 × 2 losers)
        //   * Losers: -$90 each ($30 × 3 winners)

        // Check Week 1 scores
        for (int i = 0; i < 4; i++)
        {
            // First 4 users are winners in Week 1
            Assert.Equal(10, result[i].WeekResults[0].Score);
        }
        // Last user is loser in Week 1
        Assert.Equal(-40, result[4].WeekResults[0].Score);

        // Check Week 2 scores - all should be 0 (all lost)
        for (int i = 0; i < 5; i++)
        {
            Assert.Equal(0, result[i].WeekResults[1].Score);
        }

        // Check Week 3 scores - all should be 0 (all won)
        for (int i = 0; i < 5; i++)
        {
            Assert.Equal(0, result[i].WeekResults[2].Score);
        }

        // Check Week 4 scores - juice should be $30 now
        for (int i = 0; i < 3; i++)
        {
            // First 3 users are winners in Week 4
            Assert.Equal(60, result[i].WeekResults[3].Score);
        }
        for (int i = 3; i < 5; i++)
        {
            // Last 2 users are losers in Week 4
            Assert.Equal(-90, result[i].WeekResults[3].Score);
        }

        // Check totals for all users
        // Winners in week 1 and 4: +$10 + $0 + $0 + $60 = $70
        for (int i = 0; i < 3; i++)
        {
            Assert.Equal(70, result[i].Total);
        }
        // Winner in week 1, loser in week 4: +$10 + $0 + $0 - $90 = -$80
        Assert.Equal(-80, result[3].Total);
        // Loser in week 1 and 4: -$40 + $0 + $0 - $90 = -$130
        Assert.Equal(-130, result[4].Total);

        // Verify sum of all user totals is zero (closed system)
        Assert.Equal(0, result.Sum(u => u.Total));
    }

    // ─── CFB Leaderboard Tests ────────────────────────────────────────────────

    private static (ILeagueRepository repo, ICfbRepository cfbRepo, ICfbPicksRepository picksRepo)
        BuildCfbMocks(string userId, int leagueId = 1, int slateId = 1, int slateNumber = 1) {
        var user = new ApplicationUser { Id = userId, UserName = "Alice" };
        var leagueInfo = new LeagueInfo { Id = leagueId, LeagueName = "CFB Test", OwnerUserId = userId };
        var userMapping = new LeagueUserMapping { LeagueId = leagueId, League = leagueInfo, User = user, UserId = userId };
        var juiceMapping = new LeagueJuiceMapping { Id = 1, LeagueId = leagueId, Season = 2025, Juice = 5, JuiceDivisional = 10, JuiceConference = 6, WeeklyCost = 5 };
        var slate = new CfbSlates { Id = slateId, Season = 2025, SlateNumber = slateNumber, SlateType = "RegularSeason", Label = $"Week {slateNumber}", StartDate = DateOnly.FromDateTime(DateTime.Today), EndDate = DateOnly.FromDateTime(DateTime.Today.AddDays(6)) };

        var leagueRepo = Substitute.For<ILeagueRepository>();
        leagueRepo.GetLeagueUserMappingsAsync(leagueId).Returns([userMapping]);
        leagueRepo.GetLeagueJuiceMappingAsync(leagueId, 2025).Returns(juiceMapping);

        var cfbRepo = Substitute.For<ICfbRepository>();
        cfbRepo.GetSlatesForSeasonAsync(2025).Returns([slate]);
        cfbRepo.GetSpreadsForSlateAsync(slateId).Returns((IEnumerable<CfbSpreads>)[
            new CfbSpreads { Id = 1, CfbSlateId = slateId, EspnEventId = 100, HomeTeam = "IU", AwayTeam = "OSU", HomeTeamSpread = -7, AwayTeamSpread = 7, OverUnder = 50 }
        ]);
        cfbRepo.GetScoresForSlateAsync(slateId).Returns((IEnumerable<CfbScores>)[
            new CfbScores { Id = 1, CfbSlateId = slateId, EspnEventId = 100, HomeTeam = "IU", AwayTeam = "OSU", HomeTeamScore = 28, AwayTeamScore = 14, GameStatus = "StatusFinal" }
        ]);

        var picksRepo = Substitute.For<ICfbPicksRepository>();
        picksRepo.GetUserPicksAsync(leagueId, slateId, userId).Returns((IEnumerable<CfbPicks>)[
            new CfbPicks { UserId = userId, LeagueId = leagueId, CfbSlateId = slateId, EspnEventId = 100, Team = "IU", PickType = "Spread", Season = 2025 }
        ]);

        return (leagueRepo, cfbRepo, picksRepo);
    }

    [Fact]
    public async Task CfbBuildLeaderboard_ReturnsWon_WhenAllPicksBeatSpread() {
        var userId = Guid.NewGuid().ToString();
        // slateNumber=19 = CFP National Championship → requires 1 pick, so 1 winning pick → Won
        var (leagueRepo, cfbRepo, picksRepo) = BuildCfbMocks(userId, slateNumber: 19);
        var service = new CfbLeaderboardService(new LoggerFactory().CreateLogger<CfbLeaderboardService>(), leagueRepo, cfbRepo, picksRepo);

        var result = await service.BuildLeaderboard(1, 2025);

        Assert.Single(result);
        Assert.Equal(WeekResult.Won, result[0].WeekResults[0].WeekResult);
    }

    [Fact]
    public async Task CfbBuildLeaderboard_ReturnsLost_WhenPickLosesSpread() {
        var userId = Guid.NewGuid().ToString();
        var (leagueRepo, cfbRepo, picksRepo) = BuildCfbMocks(userId);

        // Override scores: IU loses badly — 7 to 35, so IU 7 + (-7+5) = 5; 5 - 35 = -30 < 0 → loss
        cfbRepo.GetScoresForSlateAsync(1).Returns((IEnumerable<CfbScores>)[
            new CfbScores { Id = 1, CfbSlateId = 1, EspnEventId = 100, HomeTeam = "IU", AwayTeam = "OSU", HomeTeamScore = 7, AwayTeamScore = 35, GameStatus = "StatusFinal" }
        ]);

        var service = new CfbLeaderboardService(new LoggerFactory().CreateLogger<CfbLeaderboardService>(), leagueRepo, cfbRepo, picksRepo);
        var result = await service.BuildLeaderboard(1, 2025);

        Assert.Equal(WeekResult.Lost, result[0].WeekResults[0].WeekResult);
    }

    [Fact]
    public async Task CfbBuildLeaderboard_ReturnsMissingPicks_WhenUserHasFewerPicksThanGames() {
        var userId = Guid.NewGuid().ToString();
        var (leagueRepo, cfbRepo, picksRepo) = BuildCfbMocks(userId);

        // Add a second game that the user did NOT pick
        cfbRepo.GetSpreadsForSlateAsync(1).Returns((IEnumerable<CfbSpreads>)[
            new CfbSpreads { Id = 1, CfbSlateId = 1, EspnEventId = 100, HomeTeam = "IU", AwayTeam = "OSU", HomeTeamSpread = -7, AwayTeamSpread = 7, OverUnder = 50 },
            new CfbSpreads { Id = 2, CfbSlateId = 1, EspnEventId = 101, HomeTeam = "MIC", AwayTeam = "PSU", HomeTeamSpread = -3, AwayTeamSpread = 3, OverUnder = 47 },
        ]);

        var service = new CfbLeaderboardService(new LoggerFactory().CreateLogger<CfbLeaderboardService>(), leagueRepo, cfbRepo, picksRepo);
        var result = await service.BuildLeaderboard(1, 2025);

        Assert.Equal(WeekResult.MissingPicks, result[0].WeekResults[0].WeekResult);
    }

    private static (ILeagueRepository repo, ICfbRepository cfbRepo, ICfbPicksRepository picksRepo)
        BuildCfbMultiUserMocks(IReadOnlyList<string> winnerIds, IReadOnlyList<string> loserIds,
            int slateNumber = 19, int weeklyCost = 5) {
        // winnerIds pick IU (home, spread=-7): 28-7+juice-14>0 always (decisive win at any juice≥0).
        // loserIds pick OSU (away, spread=+7): 14+7+juice-28<0 for juice<7; JuiceConference=6<7 → loses.
        const int leagueId = 1;
        const int slateId = 1;
        var leagueInfo = new LeagueInfo { Id = leagueId, LeagueName = "CFB Test", OwnerUserId = winnerIds[0] };
        var userMappings = winnerIds.Concat(loserIds).Select(uid =>
            new LeagueUserMapping {
                LeagueId = leagueId, League = leagueInfo,
                User = new ApplicationUser { Id = uid, UserName = uid },
                UserId = uid
            }).ToList();
        var juiceMapping = new LeagueJuiceMapping {
            Id = 1, LeagueId = leagueId, Season = 2025,
            Juice = 5, JuiceDivisional = 10, JuiceConference = 6, WeeklyCost = weeklyCost
        };
        var slate = new CfbSlates {
            Id = slateId, Season = 2025, SlateNumber = slateNumber, SlateType = "Championship",
            Label = "Championship", StartDate = DateOnly.FromDateTime(DateTime.Today),
            EndDate = DateOnly.FromDateTime(DateTime.Today.AddDays(6))
        };

        var leagueRepo = Substitute.For<ILeagueRepository>();
        leagueRepo.GetLeagueUserMappingsAsync(leagueId).Returns(userMappings);
        leagueRepo.GetLeagueJuiceMappingAsync(leagueId, 2025).Returns(juiceMapping);

        var cfbRepo = Substitute.For<ICfbRepository>();
        cfbRepo.GetSlatesForSeasonAsync(2025).Returns([slate]);
        cfbRepo.GetSpreadsForSlateAsync(slateId).Returns((IEnumerable<CfbSpreads>)[
            new CfbSpreads { Id = 1, CfbSlateId = slateId, EspnEventId = 100,
                HomeTeam = "IU", AwayTeam = "OSU", HomeTeamSpread = -7, AwayTeamSpread = 7, OverUnder = 50 }
        ]);
        cfbRepo.GetScoresForSlateAsync(slateId).Returns((IEnumerable<CfbScores>)[
            new CfbScores { Id = 1, CfbSlateId = slateId, EspnEventId = 100,
                HomeTeam = "IU", AwayTeam = "OSU", HomeTeamScore = 28, AwayTeamScore = 14, GameStatus = "StatusFinal" }
        ]);

        var picksRepo = Substitute.For<ICfbPicksRepository>();
        picksRepo.GetUserPicksAsync(leagueId, slateId, Arg.Is<string>(uid => winnerIds.Contains(uid)))
            .Returns((IEnumerable<CfbPicks>)[
                new CfbPicks { CfbSlateId = slateId, EspnEventId = 100, Team = "IU", PickType = "Spread", Season = 2025 }
            ]);
        picksRepo.GetUserPicksAsync(leagueId, slateId, Arg.Is<string>(uid => loserIds.Contains(uid)))
            .Returns((IEnumerable<CfbPicks>)[
                new CfbPicks { CfbSlateId = slateId, EspnEventId = 100, Team = "OSU", PickType = "Spread", Season = 2025 }
            ]);

        return (leagueRepo, cfbRepo, picksRepo);
    }

    // Slates 18 (Semifinals) and 19 (Championship) must both use JuiceConference, not 0.
    // Borderline game: IU 28-20, spread -10. juice > 2 required to cover.
    // JuiceConference=6 → WIN; juice=0 (old _ => 0 bug on slate 19) → LOSE.
    [Theory]
    [InlineData(18)]
    [InlineData(19)]
    public async Task CfbBuildLeaderboard_SemiAndChampionshipUseConferenceTease(int slateNumber) {
        var userId = Guid.NewGuid().ToString();
        var (leagueRepo, cfbRepo, picksRepo) = BuildCfbMocks(userId, slateNumber: slateNumber);

        cfbRepo.GetSpreadsForSlateAsync(1).Returns((IEnumerable<CfbSpreads>)[
            new CfbSpreads { Id = 1, CfbSlateId = 1, EspnEventId = 100,
                HomeTeam = "IU", AwayTeam = "OSU", HomeTeamSpread = -10, AwayTeamSpread = 10, OverUnder = 50 }
        ]);
        cfbRepo.GetScoresForSlateAsync(1).Returns((IEnumerable<CfbScores>)[
            new CfbScores { Id = 1, CfbSlateId = 1, EspnEventId = 100,
                HomeTeam = "IU", AwayTeam = "OSU", HomeTeamScore = 28, AwayTeamScore = 20, GameStatus = "StatusFinal" }
        ]);

        var service = new CfbLeaderboardService(new LoggerFactory().CreateLogger<CfbLeaderboardService>(), leagueRepo, cfbRepo, picksRepo);
        var result = await service.BuildLeaderboard(1, 2025);

        // 28 + (-10) + JuiceConference(6) - 20 = 4 > 0 → Won
        Assert.Equal(WeekResult.Won, result[0].WeekResults[0].WeekResult);
    }

    // winner earns losers.Count × WeeklyCost; loser owes winners.Count × WeeklyCost; totals sum to 0
    [Theory]
    [InlineData(3, 2, 5,  10,  -15)]   // 3W 2L at $5  → W=+$10, L=-$15, sum=0
    [InlineData(4, 1, 10, 10,  -40)]   // 4W 1L at $10 → W=+$10, L=-$40, sum=0
    public async Task CfbCalculateTotals_ScoresBalance_WinnersAndLosers(
        int winnerCount, int loserCount, int weeklyCost, long expectedWinnerScore, long expectedLoserScore) {
        var winnerIds = Enumerable.Range(0, winnerCount).Select(_ => Guid.NewGuid().ToString()).ToList();
        var loserIds  = Enumerable.Range(0, loserCount).Select(_ => Guid.NewGuid().ToString()).ToList();
        var (leagueRepo, cfbRepo, picksRepo) = BuildCfbMultiUserMocks(winnerIds, loserIds, weeklyCost: weeklyCost);

        var service = new CfbLeaderboardService(new LoggerFactory().CreateLogger<CfbLeaderboardService>(), leagueRepo, cfbRepo, picksRepo);
        var result = await service.BuildLeaderboard(1, 2025);

        Assert.Equal(winnerCount + loserCount, result.Count);
        Assert.Equal(0, result.Sum(u => u.Total));
        Assert.All(result.Where(u => winnerIds.Contains(u.User.Id)),
            u => Assert.Equal(expectedWinnerScore, u.Total));
        Assert.All(result.Where(u => loserIds.Contains(u.User.Id)),
            u => Assert.Equal(expectedLoserScore, u.Total));
    }

    [Fact]
    public async Task CfbBuildLeaderboard_ReturnsEmpty_WhenLeagueIdIsZero() {
        var leagueRepo = Substitute.For<ILeagueRepository>();
        var cfbRepo = Substitute.For<ICfbRepository>();
        var picksRepo = Substitute.For<ICfbPicksRepository>();
        var service = new CfbLeaderboardService(new LoggerFactory().CreateLogger<CfbLeaderboardService>(), leagueRepo, cfbRepo, picksRepo);

        var result = await service.BuildLeaderboard(0, 2025);

        Assert.Empty(result);
    }

    private static async Task<bool[]> FakePlayoffPicks(SpreadCalculatorBuilder spreadCalculatorBuilder,
        ApplicationDbContext dbContext,
        List<NflScores> scores) {
        // [0] = Week 19 Wild Card, [1] = Week 20 Divisional, [2] = Week 21 Conf. Championship
        // (Week 22 = Pro Bowl, skipped; Super Bowl = Week 23, not seeded in test data)
        var isWinner = new bool[3];
        var random = new Random();

        int[] playoffWeeks = [19, 20, 21];
        for (int wi = 0; wi < playoffWeeks.Length; wi++) {
            int week = playoffWeeks[wi];
            int requiredPicks = GameHelpers.GetRequiredPicks(week);

            // Set all picks as winners initially
            bool winWeekend = true;

            // Configure a spread calculator for this week
            var spreadCalculator = await spreadCalculatorBuilder
                .WithLeagueId(dbContext.LeagueInfo.First().Id)
                .WithWeek(week)
                .WithSeason(2024)
                .BuildAsync();

            // Get scores for this week and take only what we need based on required picks
            var weekScores = scores.Where(y => y.NflWeek == week).Take(requiredPicks).ToList();
            Log.Information("Week {Week}: {WeekScoresCount} games", week, weekScores.Count);
            // Process each game for this week
            foreach (var score in weekScores) {
                // Randomly choose pick type (Spread, Over, Under)
                var pickType = (PickType)random.Next(0, Enum.GetValues<PickType>().Length);

                // Determine if home or away team is the pick
                bool pickHome = random.Next(0, 2) == 0;
                string team = pickHome ? score.HomeTeam : score.AwayTeam;
                int teamScore = pickHome ? score.HomeTeamScore : score.AwayTeamScore;
                int otherTeamScore = pickHome ? score.AwayTeamScore : score.HomeTeamScore;

                // Check if the pick is a winner
                bool isPickWinner = spreadCalculator.DidUserWinPick(team, teamScore, otherTeamScore, pickType);
                Log.Information("Pick {Team} {PickType} {IsPickWinner} {TeamScore} {OtherTeamScore}", team, pickType, isPickWinner, teamScore, otherTeamScore);
                // If any pick is a loser, the whole week is a loser
                if (!isPickWinner) {
                    winWeekend = false;
                }

                // Add the pick to the database
                dbContext.NflPicks.Add(new NflPicks() {
                    UserId = dbContext.Users.First().Id,
                    Season = 2024,
                    LeagueId = dbContext.LeagueInfo.First().Id,
                    NflWeek = week,
                    Team = team,
                    Pick = pickType
                });
            }

            isWinner[wi] = winWeekend;
        }

        await dbContext.SaveChangesAsync();
        return isWinner;
    }

    private static async Task<bool[]> FakePicks(SpreadCalculatorBuilder spreadCalculatorBuilder,
        ApplicationDbContext dbContext,
        List<NflScores> scores, int weeks) {
        var isWinner = new bool[weeks];
        for (var x = 1; x <= weeks; x++) {
            // Fake 4 Weeks Wins
            var winWeekend = true;
            var spreadCalculator = await spreadCalculatorBuilder.WithLeagueId(dbContext.LeagueInfo.First().Id)
                .WithWeek(x).WithSeason(2024).BuildAsync();
            foreach (var score in scores.Where(y => y.NflWeek == x).Take(4)) {
                var homeWinner =
                    spreadCalculator.DidUserWinPick(score.HomeTeam, score.HomeTeamScore, score.AwayTeamScore);
                var awayWinner =
                    spreadCalculator.DidUserWinPick(score.AwayTeam, score.AwayTeamScore, score.HomeTeamScore);
                if (!homeWinner && !awayWinner) {
                    winWeekend = false;
                }

                dbContext.NflPicks.Add(
                    new NflPicks {
                        UserId = dbContext.Users.First().Id,
                        Season = 2024,
                        LeagueId = dbContext.LeagueInfo.First().Id,
                        NflWeek = score.NflWeek,
                        Team =
                            awayWinner
                                ? score.AwayTeam
                                : score.HomeTeam
                    }
                );
            }

            isWinner[x - 1] = winWeekend;

        }

        await dbContext.SaveChangesAsync();
        return isWinner;
    }

}
