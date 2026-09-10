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

        var service = new LeaderboardService(new LoggerFactory().CreateLogger<LeaderboardService>(), scopeFactory, repository, TimeProvider.System);

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
        var service = new LeaderboardService(new LoggerFactory().CreateLogger<LeaderboardService>(), scopeFactory, repository, TimeProvider.System);
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
        var service = new LeaderboardService(new LoggerFactory().CreateLogger<LeaderboardService>(), scopeFactory, repository, TimeProvider.System);

        var result = await service.BuildLeaderboard(1, 2024);

        Assert.Equal(isWinner[0] ? WeekResult.Won : WeekResult.Lost, result.First().WeekResults[0].WeekResult);
        Assert.Equal(isWinner[1] ? WeekResult.Won : WeekResult.Lost, result.First().WeekResults[1].WeekResult);
        Assert.Equal(isWinner[2] ? WeekResult.Won : WeekResult.Lost, result.First().WeekResults[2].WeekResult);
        Assert.Equal(isWinner[3] ? WeekResult.Won : WeekResult.Lost, result.First().WeekResults[3].WeekResult);
        Assert.Equal(WeekResult.MissingPicks, result.First().WeekResults[4].WeekResult);

    }

    // frizat-o3x: a league with StartWeek > 1 must mark every week before it Excluded — never
    // MissingPicks/MissingGameResults, which LeaderboardSettlementHelper would otherwise (before
    // this feature) risk treating as a false all-lost push and doubling the pot for the league's
    // real first week.
    [Fact]
    public async Task CalculateRegularSeasonPicks_MarksWeeksBeforeStartWeekAsExcluded_NotMissingPicks() {
        var dbFactory = new DbContextFactoryStub();
        await dbFactory.PopulateUserTestData();
        await dbFactory.PopulateScoresTestDataAsync();
        var dbContext = await dbFactory.CreateDbContextAsync();
        var juiceMapping = dbContext.LeagueJuiceMapping.Single();
        juiceMapping.StartWeek = 2;
        await dbContext.SaveChangesAsync();

        var repository = new LeagueRepository(dbFactory);
        var spreadCalculatorBuilder =
            new SpreadCalculatorBuilder(repository, new MemoryCache(new MemoryCacheOptions()));
        var scopeFactory = BuildScopeFactory(spreadCalculatorBuilder);
        var service = new LeaderboardService(new LoggerFactory().CreateLogger<LeaderboardService>(), scopeFactory, repository, TimeProvider.System);

        var result = await service.BuildLeaderboard(1, 2024);

        Assert.Equal(WeekResult.Excluded, result.First().WeekResults[0].WeekResult); // week 1
        Assert.NotEqual(WeekResult.Excluded, result.First().WeekResults[1].WeekResult); // week 2 — real
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
        var service = new LeaderboardService(new LoggerFactory().CreateLogger<LeaderboardService>(), scopeFactory, repository, TimeProvider.System);
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
        var service = new LeaderboardService(new LoggerFactory().CreateLogger<LeaderboardService>(), scopeFactory, repository, TimeProvider.System);
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

    // frizat-tf1: a user still awaiting a final score for the week isn't a loser yet — settling
    // their week now (and paying out on an incomplete picture) would have to be silently
    // re-computed once the score lands. Same fix as the CFB carve-out above, NFL side.
    [Fact]
    public async Task CalculateUserTotals_DoesNotSettle_WhenAnyUserHasMissingGameResultsForTheWeek() {
        var dbFactory = new DbContextFactoryStub();
        await dbFactory.PopulateUserTestData(2);
        await dbFactory.PopulateScoresTestDataAsync(1);
        var repository = new LeagueRepository(dbFactory);
        var spreadCalculatorBuilder = new SpreadCalculatorBuilder(repository, new MemoryCache(new MemoryCacheOptions()));
        var dbContext = await dbFactory.CreateDbContextAsync();

        var league = dbContext.LeagueJuiceMapping.First();
        league.WeeklyCost = 10;
        await dbContext.SaveChangesAsync();

        var users = dbContext.Users.Take(2).ToList();
        var leaderboard = new List<LeaderboardModel> {
            new() { User = users[0], WeekResults = [new LeaderboardWeekResults { Week = 1, WeekResult = WeekResult.Won }] },
            new() { User = users[1], WeekResults = [new LeaderboardWeekResults { Week = 1, WeekResult = WeekResult.MissingGameResults }] },
        };

        var scopeFactory = BuildScopeFactory(spreadCalculatorBuilder);
        var service = new LeaderboardService(new LoggerFactory().CreateLogger<LeaderboardService>(), scopeFactory, repository, TimeProvider.System);
        var result = await service.CalculateUserTotals(leaderboard, league.Id, 2024, 1);

        Assert.All(result, u => Assert.Equal(0, u.WeekResults[0].Score));
        Assert.Equal(0, result.Sum(u => u.Total));
    }

    // Once at least one decided winner AND one decided loser exist, they settle against each
    // other provisionally even while a third user's pick is still pending — only the pending
    // user's own outcome stays unresolved.
    [Fact]
    public async Task CalculateUserTotals_SettlesDecidedUsersProvisionally_WhenOneUserIsStillPending() {
        var dbFactory = new DbContextFactoryStub();
        await dbFactory.PopulateUserTestData(3);
        await dbFactory.PopulateScoresTestDataAsync(1);
        var repository = new LeagueRepository(dbFactory);
        var spreadCalculatorBuilder = new SpreadCalculatorBuilder(repository, new MemoryCache(new MemoryCacheOptions()));
        var dbContext = await dbFactory.CreateDbContextAsync();

        var league = dbContext.LeagueJuiceMapping.First();
        league.WeeklyCost = 10;
        await dbContext.SaveChangesAsync();

        var users = dbContext.Users.Take(3).ToList();
        var leaderboard = new List<LeaderboardModel> {
            new() { User = users[0], WeekResults = [new LeaderboardWeekResults { Week = 1, WeekResult = WeekResult.Won }] },
            new() { User = users[1], WeekResults = [new LeaderboardWeekResults { Week = 1, WeekResult = WeekResult.Lost }] },
            new() { User = users[2], WeekResults = [new LeaderboardWeekResults { Week = 1, WeekResult = WeekResult.MissingGameResults }] },
        };

        var scopeFactory = BuildScopeFactory(spreadCalculatorBuilder);
        var service = new LeaderboardService(new LoggerFactory().CreateLogger<LeaderboardService>(), scopeFactory, repository, TimeProvider.System);
        var result = await service.CalculateUserTotals(leaderboard, league.Id, 2024, 1);

        Assert.Equal(10, result.Single(u => u.User.Id == users[0].Id).WeekResults[0].Score);
        Assert.Equal(-10, result.Single(u => u.User.Id == users[1].Id).WeekResults[0].Score);
        Assert.Equal(0, result.Single(u => u.User.Id == users[2].Id).WeekResults[0].Score);
    }

    // frizat-tf1: picks can be submitted for any individual game right up until that game's own
    // kickoff — an incomplete pick set must not be treated as a terminal loss while any of that
    // week's games hasn't started yet (e.g. Thursday Night Football already final, Sunday's games
    // still open). Mirrors the identical CFB fix above. Both callers below share the same fixed
    // kickoff times and zero picks (required picks for week 1 is 4) — only the FakeTimeProvider
    // they inject differs, which is what actually distinguishes the two scenarios.
    private static readonly DateTimeOffset NflFinishedGameTime = new(2026, 9, 11, 20, 0, 0, TimeSpan.Zero); // Thu final
    private static readonly DateTimeOffset NflPendingGameTime = new(2026, 9, 14, 13, 0, 0, TimeSpan.Zero);  // Sun, not started

    private static (ILeagueRepository repo, ISpreadCalculatorBuilder spreadCalculatorBuilder) BuildNflKickoffMocks(string userId) {
        var league = new LeagueInfo { Id = 1, LeagueName = "NFL Test", OwnerUserId = userId };
        var mapping = new LeagueUserMapping { LeagueId = 1, League = league, User = new ApplicationUser { Id = userId, UserName = "Alice" }, UserId = userId };
        var juice = new LeagueJuiceMapping { LeagueId = 1, Season = 2024, Juice = 5, JuiceDivisional = 10, JuiceConference = 6, WeeklyCost = 5 };

        var repo = Substitute.For<ILeagueRepository>();
        repo.GetLeagueUserMappingsAsync(1).Returns([mapping]);
        repo.GetLeagueJuiceMappingAsync(1).Returns([juice]);
        repo.GetAllNflScoresForSeasonAsync(2024).Returns([
            new NflScores { Season = 2024, NflWeek = 1, HomeTeam = "KC", AwayTeam = "BAL", HomeTeamScore = 27, AwayTeamScore = 20, GameTime = NflFinishedGameTime },
        ]);
        repo.GetAllNflSpreadsForSeasonAsync(2024).Returns([
            new NflSpreads { Season = 2024, NflWeek = 1, HomeTeam = "KC", AwayTeam = "BAL", HomeTeamSpread = -3, AwayTeamSpread = 3, OverUnder = 45, GameTime = NflFinishedGameTime },
            new NflSpreads { Season = 2024, NflWeek = 1, HomeTeam = "SF", AwayTeam = "DAL", HomeTeamSpread = -3, AwayTeamSpread = 3, OverUnder = 45, GameTime = NflPendingGameTime },
        ]);
        repo.GetUserNflPicksAsync(userId, 1, 2024, 1).Returns(new List<NflPicks>());

        var spreadCalculatorBuilder = Substitute.For<ISpreadCalculatorBuilder>();
        spreadCalculatorBuilder.WithLeagueId(Arg.Any<int>()).Returns(spreadCalculatorBuilder);
        spreadCalculatorBuilder.WithWeek(Arg.Any<int>()).Returns(spreadCalculatorBuilder);
        spreadCalculatorBuilder.WithSeason(Arg.Any<int>()).Returns(spreadCalculatorBuilder);
        spreadCalculatorBuilder.BuildAsync().Returns(Substitute.For<ISpreadCalculator>());

        return (repo, spreadCalculatorBuilder);
    }

    [Fact]
    public async Task NflBuildLeaderboard_ReturnsMissingGameResults_NotMissingPicks_WhenGamesHaventStartedYet() {
        var userId = Guid.NewGuid().ToString();
        var (repo, spreadCalculatorBuilder) = BuildNflKickoffMocks(userId);

        var timeProvider = new FakeTimeProvider(NflFinishedGameTime.AddHours(1)); // after Thu, before Sun
        var scopeFactory = BuildScopeFactory(spreadCalculatorBuilder);
        var service = new LeaderboardService(new LoggerFactory().CreateLogger<LeaderboardService>(), scopeFactory, repo, timeProvider);

        var result = await service.BuildLeaderboard(1, 2024);

        Assert.Equal(WeekResult.MissingGameResults, result[0].WeekResults[0].WeekResult);
    }

    [Fact]
    public async Task NflBuildLeaderboard_ReturnsMissingPicks_OnceEveryGameHasKickedOff() {
        var userId = Guid.NewGuid().ToString();
        var (repo, spreadCalculatorBuilder) = BuildNflKickoffMocks(userId);

        var timeProvider = new FakeTimeProvider(NflPendingGameTime.AddHours(1)); // after every kickoff
        var scopeFactory = BuildScopeFactory(spreadCalculatorBuilder);
        var service = new LeaderboardService(new LoggerFactory().CreateLogger<LeaderboardService>(), scopeFactory, repo, timeProvider);

        var result = await service.BuildLeaderboard(1, 2024);

        Assert.Equal(WeekResult.MissingPicks, result[0].WeekResults[0].WeekResult);
    }

    // ─── CFB Leaderboard Tests ────────────────────────────────────────────────

    // Defaults to "current slate = slate 20 of season 2025" — past every SlateNumber any existing
    // single-slate test uses, so the new current-slate clamp (frizat: leaderboard nitpicks) is a
    // no-op for all of them unless a test overrides currentSlateService itself.
    private static ICfbCurrentSlateService BuildCurrentSlateService(int? season = 2025, int slateNumber = 20) {
        var svc = Substitute.For<ICfbCurrentSlateService>();
        svc.GetCurrentSlateAsync().Returns(season is int s
            ? new CfbSlateInfo(999, s, slateNumber, "Stub", "RegularSeason",
                DateOnly.FromDateTime(DateTime.Today), DateOnly.FromDateTime(DateTime.Today), null, DateTime.UtcNow)
            : null);
        return svc;
    }

    private static (ILeagueRepository repo, ICfbRepository cfbRepo, ICfbPicksRepository picksRepo, ICfbCurrentSlateService currentSlateService)
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
            new CfbSpreads { Id = 1, CfbSlateId = slateId, HomeTeam = "IU", AwayTeam = "OSU", HomeTeamSpread = -7, AwayTeamSpread = 7, OverUnder = 50, IsLeagueEligible = true }
        ]);
        cfbRepo.GetScoresForSlateAsync(slateId).Returns((IEnumerable<CfbScores>)[
            new CfbScores { Id = 1, CfbSlateId = slateId, HomeTeam = "IU", AwayTeam = "OSU", HomeTeamScore = 28, AwayTeamScore = 14, GameStatus = TypeName.StatusFinal }
        ]);

        var picksRepo = Substitute.For<ICfbPicksRepository>();
        picksRepo.GetUserPicksAsync(leagueId, slateId, userId).Returns((IEnumerable<CfbPicks>)[
            new CfbPicks { UserId = userId, LeagueId = leagueId, CfbSlateId = slateId, Team = "IU", PickType = PickType.Spread, Season = 2025 }
        ]);

        return (leagueRepo, cfbRepo, picksRepo, BuildCurrentSlateService());
    }

    [Fact]
    public async Task CfbBuildLeaderboard_ReturnsWon_WhenAllPicksBeatSpread() {
        var userId = Guid.NewGuid().ToString();
        // slateNumber=19 = CFP National Championship → requires 1 pick, so 1 winning pick → Won
        var (leagueRepo, cfbRepo, picksRepo, currentSlateService) = BuildCfbMocks(userId, slateNumber: 19);
        var service = new CfbLeaderboardService(new LoggerFactory().CreateLogger<CfbLeaderboardService>(), leagueRepo, cfbRepo, picksRepo, currentSlateService, TimeProvider.System);

        var result = await service.BuildLeaderboard(1, 2025);

        Assert.Single(result);
        Assert.Equal(WeekResult.Won, result[0].WeekResults[0].WeekResult);
    }

    // frizat-o3x: mirrors CalculateRegularSeasonPicks_MarksWeeksBeforeStartWeekAsExcluded on the
    // CFB side — a league with StartWeek=2 must mark slate 1 Excluded, never MissingPicks.
    [Fact]
    public async Task CfbBuildLeaderboard_MarksSlatesBeforeStartWeekAsExcluded_NotMissingPicks() {
        var userId = Guid.NewGuid().ToString();
        var (leagueRepo, cfbRepo, picksRepo, currentSlateService) = BuildCfbMocks(userId);

        leagueRepo.GetLeagueJuiceMappingAsync(1, 2025).Returns(
            new LeagueJuiceMapping { Id = 1, LeagueId = 1, Season = 2025, Juice = 5, JuiceDivisional = 10, JuiceConference = 6, WeeklyCost = 5, StartWeek = 2 });

        var slate1 = new CfbSlates { Id = 1, Season = 2025, SlateNumber = 1, SlateType = "RegularSeason", Label = "Week 1", StartDate = DateOnly.FromDateTime(DateTime.Today), EndDate = DateOnly.FromDateTime(DateTime.Today.AddDays(6)) };
        var slate2 = new CfbSlates { Id = 2, Season = 2025, SlateNumber = 2, SlateType = "RegularSeason", Label = "Week 2", StartDate = DateOnly.FromDateTime(DateTime.Today), EndDate = DateOnly.FromDateTime(DateTime.Today.AddDays(6)) };
        cfbRepo.GetSlatesForSeasonAsync(2025).Returns([slate1, slate2]);
        // Slate 2 needs its own spreads/scores/picks so BuildLeaderboard doesn't just see an empty
        // slate — reuse the same fixture shape slate 1 already has in BuildCfbMocks.
        cfbRepo.GetSpreadsForSlateAsync(2).Returns((IEnumerable<CfbSpreads>)[
            new CfbSpreads { Id = 2, CfbSlateId = 2, HomeTeam = "IU", AwayTeam = "OSU", HomeTeamSpread = -7, AwayTeamSpread = 7, OverUnder = 50, IsLeagueEligible = true }
        ]);
        cfbRepo.GetScoresForSlateAsync(2).Returns((IEnumerable<CfbScores>)[
            new CfbScores { Id = 2, CfbSlateId = 2, HomeTeam = "IU", AwayTeam = "OSU", HomeTeamScore = 28, AwayTeamScore = 14, GameStatus = TypeName.StatusFinal }
        ]);
        picksRepo.GetUserPicksAsync(1, 2, userId).Returns((IEnumerable<CfbPicks>)[
            new CfbPicks { UserId = userId, LeagueId = 1, CfbSlateId = 2, Team = "IU", PickType = PickType.Spread, Season = 2025 }
        ]);

        var service = new CfbLeaderboardService(new LoggerFactory().CreateLogger<CfbLeaderboardService>(), leagueRepo, cfbRepo, picksRepo, currentSlateService, TimeProvider.System);
        var result = await service.BuildLeaderboard(1, 2025);

        Assert.Equal(WeekResult.Excluded, result[0].WeekResults[0].WeekResult); // slate 1
        Assert.NotEqual(WeekResult.Excluded, result[0].WeekResults[1].WeekResult); // slate 2 — real
    }

    [Fact]
    public async Task CfbBuildLeaderboard_ReturnsLost_WhenPickLosesSpread() {
        var userId = Guid.NewGuid().ToString();
        var (leagueRepo, cfbRepo, picksRepo, currentSlateService) = BuildCfbMocks(userId);

        // Override scores: IU loses badly — 7 to 35, so IU 7 + (-7+5) = 5; 5 - 35 = -30 < 0 → loss
        cfbRepo.GetScoresForSlateAsync(1).Returns((IEnumerable<CfbScores>)[
            new CfbScores { Id = 1, CfbSlateId = 1, HomeTeam = "IU", AwayTeam = "OSU", HomeTeamScore = 7, AwayTeamScore = 35, GameStatus = TypeName.StatusFinal }
        ]);

        var service = new CfbLeaderboardService(new LoggerFactory().CreateLogger<CfbLeaderboardService>(), leagueRepo, cfbRepo, picksRepo, currentSlateService, TimeProvider.System);
        var result = await service.BuildLeaderboard(1, 2025);

        Assert.Equal(WeekResult.Lost, result[0].WeekResults[0].WeekResult);
    }

    [Fact]
    public async Task CfbBuildLeaderboard_ReturnsMissingPicks_WhenUserHasFewerPicksThanGames() {
        var userId = Guid.NewGuid().ToString();
        var (leagueRepo, cfbRepo, picksRepo, currentSlateService) = BuildCfbMocks(userId);

        // Add a second game that the user did NOT pick. Neither game sets GameTime, which defaults
        // to DateTimeOffset.MinValue — always in the past, so this is the "picking window already
        // closed" case and MissingPicks (a genuine, terminal loss) is correct here.
        cfbRepo.GetSpreadsForSlateAsync(1).Returns((IEnumerable<CfbSpreads>)[
            new CfbSpreads { Id = 1, CfbSlateId = 1, HomeTeam = "IU", AwayTeam = "OSU", HomeTeamSpread = -7, AwayTeamSpread = 7, OverUnder = 50, IsLeagueEligible = true },
            new CfbSpreads { Id = 2, CfbSlateId = 1, HomeTeam = "MIC", AwayTeam = "PSU", HomeTeamSpread = -3, AwayTeamSpread = 3, OverUnder = 47, IsLeagueEligible = true },
        ]);

        var service = new CfbLeaderboardService(new LoggerFactory().CreateLogger<CfbLeaderboardService>(), leagueRepo, cfbRepo, picksRepo, currentSlateService, TimeProvider.System);
        var result = await service.BuildLeaderboard(1, 2025);

        Assert.Equal(WeekResult.MissingPicks, result[0].WeekResults[0].WeekResult);
    }

    // frizat-8y6: reported live on prod — a slate with ZERO spreads released yet (the picking
    // window hasn't even opened) showed on the leaderboard as MissingPicks — a terminal loss —
    // for every user, because AllGamesStarted's underlying .All() is vacuously true on an empty
    // sequence. A week that hasn't started must never read as "everyone missed their picks".
    [Fact]
    public async Task CfbBuildLeaderboard_ReturnsMissingGameResults_NotMissingPicks_WhenNoSpreadsExistYet() {
        var userId = Guid.NewGuid().ToString();
        const int leagueId = 1;
        const int slateId = 1;
        var leagueInfo = new LeagueInfo { Id = leagueId, LeagueName = "CFB Test", OwnerUserId = userId };
        var userMapping = new LeagueUserMapping { LeagueId = leagueId, League = leagueInfo, User = new ApplicationUser { Id = userId, UserName = "Alice" }, UserId = userId };
        var juiceMapping = new LeagueJuiceMapping { Id = 1, LeagueId = leagueId, Season = 2025, Juice = 5, JuiceDivisional = 10, JuiceConference = 6, WeeklyCost = 5 };
        var slate = new CfbSlates { Id = slateId, Season = 2025, SlateNumber = 2, SlateType = "RegularSeason", Label = "Week 2", StartDate = DateOnly.FromDateTime(DateTime.Today), EndDate = DateOnly.FromDateTime(DateTime.Today.AddDays(6)) };

        var leagueRepo = Substitute.For<ILeagueRepository>();
        leagueRepo.GetLeagueUserMappingsAsync(leagueId).Returns([userMapping]);
        leagueRepo.GetLeagueJuiceMappingAsync(leagueId, 2025).Returns(juiceMapping);

        var cfbRepo = Substitute.For<ICfbRepository>();
        cfbRepo.GetSlatesForSeasonAsync(2025).Returns([slate]);
        cfbRepo.GetSpreadsForSlateAsync(slateId).Returns((IEnumerable<CfbSpreads>)[]); // nothing released yet
        cfbRepo.GetScoresForSlateAsync(slateId).Returns((IEnumerable<CfbScores>)[]);

        var picksRepo = Substitute.For<ICfbPicksRepository>();
        picksRepo.GetUserPicksAsync(leagueId, slateId, userId).Returns((IEnumerable<CfbPicks>)[]);

        var service = new CfbLeaderboardService(new LoggerFactory().CreateLogger<CfbLeaderboardService>(),
            leagueRepo, cfbRepo, picksRepo, BuildCurrentSlateService(slateNumber: 2), TimeProvider.System);
        var result = await service.BuildLeaderboard(leagueId, 2025);

        Assert.Equal(WeekResult.MissingGameResults, result[0].WeekResults[0].WeekResult);
        // Not a terminal loss, so no payout is computed for a week that hasn't started.
        Assert.Equal(0, result[0].WeekResults[0].Score);
    }

    // frizat-tf1: a user can submit picks for any individual game right up until that game's own
    // kickoff (CfbPicksController.StartedTeams uses the identical GameTime <= now check) — so an
    // incomplete pick set must not be treated as a terminal loss while any of that slate's games
    // hasn't started yet. Reported by a real user: shown as having lost a week before it was over.
    [Fact]
    public async Task CfbBuildLeaderboard_ReturnsMissingGameResults_NotMissingPicks_WhenGamesHaventStartedYet() {
        var userId = Guid.NewGuid().ToString();
        var (leagueRepo, cfbRepo, picksRepo, currentSlateService) = BuildCfbMocks(userId);
        var kickoff = new DateTimeOffset(2026, 9, 12, 19, 0, 0, TimeSpan.Zero);

        // Second game the user did NOT pick, kicking off in the future relative to "now".
        cfbRepo.GetSpreadsForSlateAsync(1).Returns((IEnumerable<CfbSpreads>)[
            new CfbSpreads { Id = 1, CfbSlateId = 1, HomeTeam = "IU", AwayTeam = "OSU", HomeTeamSpread = -7, AwayTeamSpread = 7, OverUnder = 50, IsLeagueEligible = true, GameTime = kickoff },
            new CfbSpreads { Id = 2, CfbSlateId = 1, HomeTeam = "MIC", AwayTeam = "PSU", HomeTeamSpread = -3, AwayTeamSpread = 3, OverUnder = 47, IsLeagueEligible = true, GameTime = kickoff },
        ]);

        var timeProvider = new FakeTimeProvider(kickoff.AddHours(-1)); // one hour before kickoff
        var service = new CfbLeaderboardService(new LoggerFactory().CreateLogger<CfbLeaderboardService>(), leagueRepo, cfbRepo, picksRepo, currentSlateService, timeProvider);
        var result = await service.BuildLeaderboard(1, 2025);

        Assert.Equal(WeekResult.MissingGameResults, result[0].WeekResults[0].WeekResult);
    }

    [Fact]
    public async Task CfbBuildLeaderboard_ReturnsMissingPicks_OnceEveryGameHasKickedOff() {
        var userId = Guid.NewGuid().ToString();
        var (leagueRepo, cfbRepo, picksRepo, currentSlateService) = BuildCfbMocks(userId);
        var kickoff = new DateTimeOffset(2026, 9, 12, 19, 0, 0, TimeSpan.Zero);

        cfbRepo.GetSpreadsForSlateAsync(1).Returns((IEnumerable<CfbSpreads>)[
            new CfbSpreads { Id = 1, CfbSlateId = 1, HomeTeam = "IU", AwayTeam = "OSU", HomeTeamSpread = -7, AwayTeamSpread = 7, OverUnder = 50, IsLeagueEligible = true, GameTime = kickoff },
            new CfbSpreads { Id = 2, CfbSlateId = 1, HomeTeam = "MIC", AwayTeam = "PSU", HomeTeamSpread = -3, AwayTeamSpread = 3, OverUnder = 47, IsLeagueEligible = true, GameTime = kickoff },
        ]);

        var timeProvider = new FakeTimeProvider(kickoff.AddHours(1)); // one hour after kickoff
        var service = new CfbLeaderboardService(new LoggerFactory().CreateLogger<CfbLeaderboardService>(), leagueRepo, cfbRepo, picksRepo, currentSlateService, timeProvider);
        var result = await service.BuildLeaderboard(1, 2025);

        Assert.Equal(WeekResult.MissingPicks, result[0].WeekResults[0].WeekResult);
    }

    // frizat-tf1: a user with 2 covering picks and 2 picks still awaiting a final score must not be
    // financially settled as a loser for the week — reused from a real prod bug report where this
    // showed a user losing money before their week was actually decided.
    [Fact]
    public async Task CfbCalculateTotals_DoesNotSettle_WhenAnyUserHasMissingGameResultsForTheWeek() {
        const int leagueId = 1;
        const int slateId = 1;
        var decidedUserId = Guid.NewGuid().ToString();
        var pendingUserId = Guid.NewGuid().ToString();
        var leagueInfo = new LeagueInfo { Id = leagueId, LeagueName = "CFB Test", OwnerUserId = decidedUserId };
        var userMappings = new List<LeagueUserMapping> {
            new() { LeagueId = leagueId, League = leagueInfo, User = new ApplicationUser { Id = decidedUserId, UserName = "Decided" }, UserId = decidedUserId },
            new() { LeagueId = leagueId, League = leagueInfo, User = new ApplicationUser { Id = pendingUserId, UserName = "Pending" }, UserId = pendingUserId },
        };
        var juiceMapping = new LeagueJuiceMapping { Id = 1, LeagueId = leagueId, Season = 2025, Juice = 5, JuiceDivisional = 10, JuiceConference = 6, WeeklyCost = 5 };
        var slate = new CfbSlates { Id = slateId, Season = 2025, SlateNumber = 19, SlateType = "Championship", Label = "Championship", StartDate = DateOnly.FromDateTime(DateTime.Today), EndDate = DateOnly.FromDateTime(DateTime.Today.AddDays(6)) };

        var leagueRepo = Substitute.For<ILeagueRepository>();
        leagueRepo.GetLeagueUserMappingsAsync(leagueId).Returns(userMappings);
        leagueRepo.GetLeagueJuiceMappingAsync(leagueId, 2025).Returns(juiceMapping);

        var cfbRepo = Substitute.For<ICfbRepository>();
        cfbRepo.GetSlatesForSeasonAsync(2025).Returns([slate]);
        // Decided user picks IU (spread exists AND has a final score). Pending user picks MIC
        // (spread exists, but no score row at all yet) — the exact shape of the real prod bug.
        cfbRepo.GetSpreadsForSlateAsync(slateId).Returns((IEnumerable<CfbSpreads>)[
            new CfbSpreads { Id = 1, CfbSlateId = slateId, HomeTeam = "IU", AwayTeam = "OSU", HomeTeamSpread = -7, AwayTeamSpread = 7, OverUnder = 50, IsLeagueEligible = true },
            new CfbSpreads { Id = 2, CfbSlateId = slateId, HomeTeam = "MIC", AwayTeam = "PSU", HomeTeamSpread = -3, AwayTeamSpread = 3, OverUnder = 47, IsLeagueEligible = true },
        ]);
        cfbRepo.GetScoresForSlateAsync(slateId).Returns((IEnumerable<CfbScores>)[
            new CfbScores { Id = 1, CfbSlateId = slateId, HomeTeam = "IU", AwayTeam = "OSU", HomeTeamScore = 28, AwayTeamScore = 14, GameStatus = TypeName.StatusFinal }
        ]);

        var picksRepo = Substitute.For<ICfbPicksRepository>();
        picksRepo.GetUserPicksAsync(leagueId, slateId, decidedUserId).Returns((IEnumerable<CfbPicks>)[
            new CfbPicks { UserId = decidedUserId, LeagueId = leagueId, CfbSlateId = slateId, Team = "IU", PickType = PickType.Spread, Season = 2025 }
        ]);
        // Pending user picked a DIFFERENT game with no score row at all — MissingGameResults.
        picksRepo.GetUserPicksAsync(leagueId, slateId, pendingUserId).Returns((IEnumerable<CfbPicks>)[
            new CfbPicks { UserId = pendingUserId, LeagueId = leagueId, CfbSlateId = slateId, Team = "MIC", PickType = PickType.Spread, Season = 2025 }
        ]);

        var service = new CfbLeaderboardService(new LoggerFactory().CreateLogger<CfbLeaderboardService>(),
            leagueRepo, cfbRepo, picksRepo, BuildCurrentSlateService(slateNumber: 19), TimeProvider.System);
        var result = await service.BuildLeaderboard(leagueId, 2025);

        var decided = result.Single(u => u.User.Id == decidedUserId);
        var pending = result.Single(u => u.User.Id == pendingUserId);
        Assert.Equal(WeekResult.Won, decided.WeekResults[0].WeekResult);
        Assert.Equal(WeekResult.MissingGameResults, pending.WeekResults[0].WeekResult);
        // Neither user gets paid or charged for a week that isn't decided yet.
        Assert.Equal(0, decided.WeekResults[0].Score);
        Assert.Equal(0, pending.WeekResults[0].Score);
    }

    // Extends the scenario above: once a decided winner AND a decided loser both exist, they
    // settle against each other provisionally even while a third user's pick is still pending.
    [Fact]
    public async Task CfbCalculateTotals_SettlesDecidedUsersProvisionally_WhenOneUserIsStillPending() {
        const int leagueId = 1;
        const int slateId = 1;
        var winnerId = Guid.NewGuid().ToString();
        var loserId = Guid.NewGuid().ToString();
        var pendingId = Guid.NewGuid().ToString();
        var leagueInfo = new LeagueInfo { Id = leagueId, LeagueName = "CFB Test", OwnerUserId = winnerId };
        var userMappings = new List<LeagueUserMapping> {
            new() { LeagueId = leagueId, League = leagueInfo, User = new ApplicationUser { Id = winnerId, UserName = "Winner" }, UserId = winnerId },
            new() { LeagueId = leagueId, League = leagueInfo, User = new ApplicationUser { Id = loserId, UserName = "Loser" }, UserId = loserId },
            new() { LeagueId = leagueId, League = leagueInfo, User = new ApplicationUser { Id = pendingId, UserName = "Pending" }, UserId = pendingId },
        };
        var juiceMapping = new LeagueJuiceMapping { Id = 1, LeagueId = leagueId, Season = 2025, Juice = 5, JuiceDivisional = 10, JuiceConference = 6, WeeklyCost = 5 };
        var slate = new CfbSlates { Id = slateId, Season = 2025, SlateNumber = 19, SlateType = "Championship", Label = "Championship", StartDate = DateOnly.FromDateTime(DateTime.Today), EndDate = DateOnly.FromDateTime(DateTime.Today.AddDays(6)) };

        var leagueRepo = Substitute.For<ILeagueRepository>();
        leagueRepo.GetLeagueUserMappingsAsync(leagueId).Returns(userMappings);
        leagueRepo.GetLeagueJuiceMappingAsync(leagueId, 2025).Returns(juiceMapping);

        var cfbRepo = Substitute.For<ICfbRepository>();
        cfbRepo.GetSlatesForSeasonAsync(2025).Returns([slate]);
        // Winner picks IU (spread + final score both exist, covers). Loser picks OSU (loses).
        // Pending picks MIC (spread exists, no score row at all yet).
        cfbRepo.GetSpreadsForSlateAsync(slateId).Returns((IEnumerable<CfbSpreads>)[
            new CfbSpreads { Id = 1, CfbSlateId = slateId, HomeTeam = "IU", AwayTeam = "OSU", HomeTeamSpread = -7, AwayTeamSpread = 7, OverUnder = 50, IsLeagueEligible = true },
            new CfbSpreads { Id = 2, CfbSlateId = slateId, HomeTeam = "MIC", AwayTeam = "PSU", HomeTeamSpread = -3, AwayTeamSpread = 3, OverUnder = 47, IsLeagueEligible = true },
        ]);
        cfbRepo.GetScoresForSlateAsync(slateId).Returns((IEnumerable<CfbScores>)[
            new CfbScores { Id = 1, CfbSlateId = slateId, HomeTeam = "IU", AwayTeam = "OSU", HomeTeamScore = 28, AwayTeamScore = 14, GameStatus = TypeName.StatusFinal }
        ]);

        var picksRepo = Substitute.For<ICfbPicksRepository>();
        picksRepo.GetUserPicksAsync(leagueId, slateId, winnerId).Returns((IEnumerable<CfbPicks>)[
            new CfbPicks { UserId = winnerId, LeagueId = leagueId, CfbSlateId = slateId, Team = "IU", PickType = PickType.Spread, Season = 2025 }
        ]);
        picksRepo.GetUserPicksAsync(leagueId, slateId, loserId).Returns((IEnumerable<CfbPicks>)[
            new CfbPicks { UserId = loserId, LeagueId = leagueId, CfbSlateId = slateId, Team = "OSU", PickType = PickType.Spread, Season = 2025 }
        ]);
        picksRepo.GetUserPicksAsync(leagueId, slateId, pendingId).Returns((IEnumerable<CfbPicks>)[
            new CfbPicks { UserId = pendingId, LeagueId = leagueId, CfbSlateId = slateId, Team = "MIC", PickType = PickType.Spread, Season = 2025 }
        ]);

        var service = new CfbLeaderboardService(new LoggerFactory().CreateLogger<CfbLeaderboardService>(),
            leagueRepo, cfbRepo, picksRepo, BuildCurrentSlateService(slateNumber: 19), TimeProvider.System);
        var result = await service.BuildLeaderboard(leagueId, 2025);

        var winner = result.Single(u => u.User.Id == winnerId);
        var loser = result.Single(u => u.User.Id == loserId);
        var pending = result.Single(u => u.User.Id == pendingId);
        Assert.Equal(WeekResult.Won, winner.WeekResults[0].WeekResult);
        Assert.Equal(WeekResult.Lost, loser.WeekResults[0].WeekResult);
        Assert.Equal(WeekResult.MissingGameResults, pending.WeekResults[0].WeekResult);
        Assert.Equal(5, winner.WeekResults[0].Score);
        Assert.Equal(-5, loser.WeekResults[0].Score);
        Assert.Equal(0, pending.WeekResults[0].Score);
    }

    // ─── CFB Leaderboard — clamp to current slate (frizat: "shows week 18 and all missed picks"
    // even for slates that haven't happened yet — CfbSlates is fully seeded ahead of time for the
    // whole season, unlike NFL's NflScores which only exist once a game is final) ────────────────

    [Fact]
    public async Task CfbBuildLeaderboard_ExcludesSlatesAfterTheCurrentOne() {
        var userId = Guid.NewGuid().ToString();
        var (leagueRepo, cfbRepo, picksRepo, _) = BuildCfbMocks(userId, slateNumber: 1);
        var futureSlate = new CfbSlates { Id = 2, Season = 2025, SlateNumber = 2, SlateType = "RegularSeason", Label = "Week 2", StartDate = DateOnly.FromDateTime(DateTime.Today.AddDays(7)), EndDate = DateOnly.FromDateTime(DateTime.Today.AddDays(13)) };
        cfbRepo.GetSlatesForSeasonAsync(2025).Returns([
            new CfbSlates { Id = 1, Season = 2025, SlateNumber = 1, SlateType = "RegularSeason", Label = "Week 1", StartDate = DateOnly.FromDateTime(DateTime.Today), EndDate = DateOnly.FromDateTime(DateTime.Today.AddDays(6)) },
            futureSlate,
        ]);
        // Current slate is still slate 1 — slate 2 hasn't happened yet.
        var currentSlateService = BuildCurrentSlateService(season: 2025, slateNumber: 1);

        var service = new CfbLeaderboardService(new LoggerFactory().CreateLogger<CfbLeaderboardService>(), leagueRepo, cfbRepo, picksRepo, currentSlateService, TimeProvider.System);
        var result = await service.BuildLeaderboard(1, 2025);

        Assert.Single(result[0].WeekResults); // only slate 1, slate 2 excluded
        Assert.Equal(1, result[0].WeekResults[0].Week);
    }

    [Fact]
    public async Task CfbBuildLeaderboard_DoesNotClamp_ForAFullyCompletedPastSeason() {
        var userId = Guid.NewGuid().ToString();
        var (leagueRepo, cfbRepo, picksRepo, _) = BuildCfbMocks(userId, slateNumber: 1);
        cfbRepo.GetSlatesForSeasonAsync(2025).Returns([
            new CfbSlates { Id = 1, Season = 2025, SlateNumber = 1, SlateType = "RegularSeason", Label = "Week 1", StartDate = DateOnly.FromDateTime(DateTime.Today), EndDate = DateOnly.FromDateTime(DateTime.Today.AddDays(6)) },
            new CfbSlates { Id = 2, Season = 2025, SlateNumber = 2, SlateType = "RegularSeason", Label = "Week 2", StartDate = DateOnly.FromDateTime(DateTime.Today.AddDays(7)), EndDate = DateOnly.FromDateTime(DateTime.Today.AddDays(13)) },
        ]);
        cfbRepo.GetSpreadsForSlateAsync(2).Returns((IEnumerable<CfbSpreads>)[]);
        cfbRepo.GetScoresForSlateAsync(2).Returns((IEnumerable<CfbScores>)[]);
        picksRepo.GetUserPicksAsync(1, 2, userId).Returns((IEnumerable<CfbPicks>)[]);
        // "Now" is season 2026 — 2025 is fully in the past, so every one of its slates should
        // still show, not be clamped to whatever slate 2025 last resolved to.
        var currentSlateService = BuildCurrentSlateService(season: 2026, slateNumber: 1);

        var service = new CfbLeaderboardService(new LoggerFactory().CreateLogger<CfbLeaderboardService>(), leagueRepo, cfbRepo, picksRepo, currentSlateService, TimeProvider.System);
        var result = await service.BuildLeaderboard(1, 2025);

        Assert.Equal(2, result[0].WeekResults.Length);
    }

    [Fact]
    public async Task CfbBuildLeaderboard_ReturnsEmpty_ForASeasonThatHasNotStartedYet() {
        var userId = Guid.NewGuid().ToString();
        var (leagueRepo, cfbRepo, picksRepo, _) = BuildCfbMocks(userId, slateNumber: 1);
        // "Now" is season 2025 — 2026 hasn't started, so nothing should show for it yet.
        var currentSlateService = BuildCurrentSlateService(season: 2025, slateNumber: 1);
        leagueRepo.GetLeagueJuiceMappingAsync(1, 2026).Returns(new LeagueJuiceMapping { Id = 2, LeagueId = 1, Season = 2026, Juice = 5, JuiceDivisional = 10, JuiceConference = 6, WeeklyCost = 5 });
        cfbRepo.GetSlatesForSeasonAsync(2026).Returns([
            new CfbSlates { Id = 3, Season = 2026, SlateNumber = 1, SlateType = "RegularSeason", Label = "Week 1", StartDate = DateOnly.FromDateTime(DateTime.Today), EndDate = DateOnly.FromDateTime(DateTime.Today.AddDays(6)) },
        ]);

        var service = new CfbLeaderboardService(new LoggerFactory().CreateLogger<CfbLeaderboardService>(), leagueRepo, cfbRepo, picksRepo, currentSlateService, TimeProvider.System);
        var result = await service.BuildLeaderboard(1, 2026);

        Assert.Empty(result);
    }

    [Fact]
    public async Task CfbBuildLeaderboard_DoesNotClamp_WhenCurrentSlateCannotBeResolved() {
        // Fail open, same philosophy as the rest of this codebase's "no data yet" handling —
        // a null current slate (e.g. pre-launch, no slates seeded anywhere) must not hide every
        // real slate that DOES exist for the requested season.
        var userId = Guid.NewGuid().ToString();
        var (leagueRepo, cfbRepo, picksRepo, _) = BuildCfbMocks(userId, slateNumber: 1);
        var currentSlateService = BuildCurrentSlateService(season: null);

        var service = new CfbLeaderboardService(new LoggerFactory().CreateLogger<CfbLeaderboardService>(), leagueRepo, cfbRepo, picksRepo, currentSlateService, TimeProvider.System);
        var result = await service.BuildLeaderboard(1, 2025);

        Assert.Single(result[0].WeekResults);
    }

    private static (ILeagueRepository repo, ICfbRepository cfbRepo, ICfbPicksRepository picksRepo, ICfbCurrentSlateService currentSlateService)
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
            new CfbSpreads { Id = 1, CfbSlateId = slateId, HomeTeam = "IU", AwayTeam = "OSU", HomeTeamSpread = -7, AwayTeamSpread = 7, OverUnder = 50, IsLeagueEligible = true }
        ]);
        cfbRepo.GetScoresForSlateAsync(slateId).Returns((IEnumerable<CfbScores>)[
            new CfbScores { Id = 1, CfbSlateId = slateId, HomeTeam = "IU", AwayTeam = "OSU", HomeTeamScore = 28, AwayTeamScore = 14, GameStatus = TypeName.StatusFinal }
        ]);

        var picksRepo = Substitute.For<ICfbPicksRepository>();
        picksRepo.GetUserPicksAsync(leagueId, slateId, Arg.Is<string>(uid => winnerIds.Contains(uid)))
            .Returns((IEnumerable<CfbPicks>)[
                new CfbPicks { CfbSlateId = slateId, Team = "IU", PickType = PickType.Spread, Season = 2025 }
            ]);
        picksRepo.GetUserPicksAsync(leagueId, slateId, Arg.Is<string>(uid => loserIds.Contains(uid)))
            .Returns((IEnumerable<CfbPicks>)[
                new CfbPicks { CfbSlateId = slateId, Team = "OSU", PickType = PickType.Spread, Season = 2025 }
            ]);

        return (leagueRepo, cfbRepo, picksRepo, BuildCurrentSlateService(slateNumber: slateNumber));
    }

    // Slates 18 (Semifinals) and 19 (Championship) must both use JuiceConference, not 0.
    // Borderline game: IU 28-20, spread -10. juice > 2 required to cover.
    // JuiceConference=6 → WIN; juice=0 (old _ => 0 bug on slate 19) → LOSE.
    [Theory]
    [InlineData(18)]
    [InlineData(19)]
    public async Task CfbBuildLeaderboard_SemiAndChampionshipUseConferenceTease(int slateNumber) {
        var userId = Guid.NewGuid().ToString();
        var (leagueRepo, cfbRepo, picksRepo, currentSlateService) = BuildCfbMocks(userId, slateNumber: slateNumber);

        cfbRepo.GetSpreadsForSlateAsync(1).Returns((IEnumerable<CfbSpreads>)[
            new CfbSpreads { Id = 1, CfbSlateId = 1, HomeTeam = "IU", AwayTeam = "OSU", HomeTeamSpread = -10, AwayTeamSpread = 10, OverUnder = 50, IsLeagueEligible = true }
        ]);
        cfbRepo.GetScoresForSlateAsync(1).Returns((IEnumerable<CfbScores>)[
            new CfbScores { Id = 1, CfbSlateId = 1, HomeTeam = "IU", AwayTeam = "OSU", HomeTeamScore = 28, AwayTeamScore = 20, GameStatus = TypeName.StatusFinal }
        ]);

        var service = new CfbLeaderboardService(new LoggerFactory().CreateLogger<CfbLeaderboardService>(), leagueRepo, cfbRepo, picksRepo, currentSlateService, TimeProvider.System);
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
        var (leagueRepo, cfbRepo, picksRepo, currentSlateService) = BuildCfbMultiUserMocks(winnerIds, loserIds, weeklyCost: weeklyCost);

        var service = new CfbLeaderboardService(new LoggerFactory().CreateLogger<CfbLeaderboardService>(), leagueRepo, cfbRepo, picksRepo, currentSlateService, TimeProvider.System);
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
        var currentSlateService = Substitute.For<ICfbCurrentSlateService>();
        var service = new CfbLeaderboardService(new LoggerFactory().CreateLogger<CfbLeaderboardService>(), leagueRepo, cfbRepo, picksRepo, currentSlateService, TimeProvider.System);

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
