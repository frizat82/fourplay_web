using AutoFixture;
using AutoFixture.AutoNSubstitute;
using AutoFixture.Xunit2;
using FourPlayWebApp.Server.Data;
using FourPlayWebApp.Server.Models.Data;
using FourPlayWebApp.Server.Models.Identity;
using FourPlayWebApp.Shared.Models.Data;
using Microsoft.EntityFrameworkCore;

namespace FourPlayWebApp.Server.UnitTests;

public class AutoNSubData() : AutoDataAttribute(() => new Fixture().Customize(new AutoNSubstituteCustomization()
    {ConfigureMembers = true})) {
    // This constructor initializes the AutoDataAttribute with a fixture that has been customized
    // to use NSubstitute for mocking dependencies.
}

public class DbContextFactoryStub : IDbContextFactory<ApplicationDbContext>
{
    private readonly TestApplicationDbContext _dbContext;
    private readonly string _dbName;

    public DbContextFactoryStub() : this("TestDb") { }

    public DbContextFactoryStub(string dbName) {
        _dbName = dbName;
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options;
        _dbContext = new TestApplicationDbContext(options);
        _dbContext.Database.EnsureDeleted();
        _dbContext.Database.EnsureCreated();
    }

    public Task<ApplicationDbContext> CreateDbContextAsync() =>
        Task.FromResult(CreateDbContext());

    public async Task PopulateScoresTestDataAsync(int weeks = 22)
    {
        var fixture = new Fixture();
        // Generate 18 weeks of NFL scores
        var scores = fixture.Build<NflScores>()
            .With(s => s.Season, 2024)
            .CreateMany(weeks * 5) // Create 5 scores for each of the 18 weeks and 4 Post Season
            .Select((score, index) => {
                score.NflWeek = (index / 5) + 1; // Assign week number (1 to 18)
                score.HomeTeam = $"Team{index % 5 + 1}H";
                score.AwayTeam = $"Team{index % 5 + 1}A";
                var randomInt = fixture.Create<int>() % 36; // Generates a number between 0 and 35
                if (randomInt < 0) randomInt += 36; // Ensures the number is non-negative
                score.HomeTeamScore = randomInt;
                randomInt = fixture.Create<int>() % 36; // Generates a number between 0 and 35
                if (randomInt < 0) randomInt += 36; // Ensures the number is non-negative
                score.AwayTeamScore = randomInt;
                score.GameTime = new DateTime(2024, 1, score.NflWeek, 12, 0, 0);
                return score;
            }).ToList();

        _dbContext.NflScores.AddRange(scores);
        // Generate 18 weeks of NFL spreads
        var spreads = fixture.Build<NflSpreads>()
            .With(s => s.Season, 2024)
            .CreateMany(weeks * 5) // Create 5 spreads for each of the 18 weeks
            .Select((spread, index) => {
                spread.NflWeek = (index / 5) + 1; // Assign week number (1 to 18)
                spread.HomeTeam = $"Team{index % 5 + 1}H";
                spread.AwayTeam = $"Team{index % 5 + 1}A";
                var randomInt = fixture.Create<int>() % 11; // Generates a number between 0 and 10
                if (randomInt < 0) randomInt += 11; // Ensures the number is non-negative
                spread.AwayTeamSpread = randomInt;
                spread.HomeTeamSpread = -randomInt;
                if (index > 18) {
                    randomInt = fixture.Create<int>() % 61; // Generates a number between 0 and 60
                    if (randomInt < 0) randomInt += 61; // Ensures the number is non-negative
                    spread.OverUnder = randomInt;
                }
                spread.GameTime = new DateTime(2024, 1, spread.NflWeek, 12, 0, 0);
                return spread;
            }).ToList();
        _dbContext.NflSpreads.AddRange(spreads);
        await _dbContext.SaveChangesAsync();
    }
    /// <summary>
    /// Populates exactly two weeks with distinct, non-overlapping teams so that
    /// concurrency tests can verify that a calculator for week N never contains
    /// teams from week M.
    /// Week 1: "W1Home" vs "W1Away"  (spread -3 / +3)
    /// Week 2: "W2Home" vs "W2Away"  (spread -7 / +7)
    /// </summary>
    public async Task PopulateTwoWeekSpreadsAsync()
    {
        _dbContext.NflSpreads.AddRange(
            new NflSpreads { Season = 2024, NflWeek = 1, HomeTeam = "W1Home", AwayTeam = "W1Away", HomeTeamSpread = -3, AwayTeamSpread = 3, OverUnder = 45 },
            new NflSpreads { Season = 2024, NflWeek = 2, HomeTeam = "W2Home", AwayTeam = "W2Away", HomeTeamSpread = -7, AwayTeamSpread = 7, OverUnder = 52 }
        );
        await _dbContext.SaveChangesAsync();
    }

    public async Task PopulateUserTestData(int userCount = 1)
    {
        var fixture = new Fixture();
        LeagueInfo? leagueInfo = null;
        for (int i = 0; i < userCount; i++)
        {
            var user = fixture.Create<ApplicationUser>();
            _dbContext.Users.Add(user);
            if (!_dbContext.LeagueInfo
                    .Any()) // Only create league info if it doesn't already exist (for testing purposes
            {
                 leagueInfo = new LeagueInfo() {LeagueName = "Test League", OwnerUserId = user.Id};
                _dbContext.LeagueInfo.Add(leagueInfo);
                await _dbContext.SaveChangesAsync();
            }

            if (!_dbContext.LeagueJuiceMapping.Any()) // Only create league juice mapping if it doesn't already exist
            {

                _dbContext.LeagueJuiceMapping.Add(
                    new LeagueJuiceMapping {
                        Season = 2024, WeeklyCost = 5, Juice = 5, JuiceConference = 10, JuiceDivisional = 15,
                        LeagueId = _dbContext.LeagueInfo.First().Id
                    }
                );
                await _dbContext.SaveChangesAsync();
            }

            _dbContext.LeagueUsers.Add(new LeagueUsers {Email = user.Email!});
            _dbContext.LeagueUserMapping.Add(new LeagueUserMapping {
                LeagueId = 1,
                League = leagueInfo!,
                User = user, UserId = user.Id
            });
        }
    }

    public ApplicationDbContext CreateDbContext() {
        return _dbContext;
    }
}

public class TestApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : ApplicationDbContext(options) {
    // This constructor is used for testing purposes.

    public override void Dispose() {

    }

    public override ValueTask DisposeAsync() {
        return new ValueTask(Task.CompletedTask);
    }
}
