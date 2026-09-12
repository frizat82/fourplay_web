using FourPlayWebApp.Server.Data;
using FourPlayWebApp.Server.Models.Data;
using FourPlayWebApp.Server.Models.Identity;
using FourPlayWebApp.Server.Services.Repositories;
using FourPlayWebApp.Shared.Models.Enum;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Testcontainers.PostgreSql;
using Xunit;

namespace FourPlayWebApp.Server.UnitTests;

/// <summary>
/// PostgreSQL-backed regression tests for TryAddNflPicksAsync's advisory-lock cap enforcement
/// (frizat-immediate-pick-toggle, GitHub issue #371).
///
/// WHY POSTGRESQL: pg_advisory_xact_lock/hashtext have no SQLite equivalent — a SQLite-backed
/// test cannot exercise the real lock at all, let alone prove it prevents a race. Only a real
/// Postgres container can demonstrate that concurrent requests racing the same (user, league,
/// season, week) pick count never both squeeze past the required-pick cap.
/// </summary>
public class PostgresPicksFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithCleanUp(true)
        .Build();

    public string ConnectionString { get; private set; } = "";

    public async Task InitializeAsync()
    {
        await _container.StartAsync();
        ConnectionString = _container.GetConnectionString();
        await using var db = OpenDb();
        await db.Database.MigrateAsync();
    }

    public async Task DisposeAsync() => await _container.DisposeAsync();

    public ApplicationDbContext OpenDb() =>
        new(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql(ConnectionString)
            .Options);
}

public class PickConcurrencyTests : IClassFixture<PostgresPicksFixture>
{
    private const int Season = 2025;
    private const int Week = 3;
    private const int RequiredPicks = 4;
    private const string TestUserId = "concurrency-test-user";

    private readonly PostgresPicksFixture _fixture;

    public PickConcurrencyTests(PostgresPicksFixture fixture)
    {
        _fixture = fixture;
    }

    // Every call must get its own DbContext instance (EF DbContext is not thread-safe) but all
    // pointed at the same underlying Postgres container, so the advisory lock is actually shared.
    private IDbContextFactory<ApplicationDbContext> BuildFactory()
    {
        var factory = Substitute.For<IDbContextFactory<ApplicationDbContext>>();
        factory.CreateDbContextAsync().Returns(_ => _fixture.OpenDb());
        return factory;
    }

    private async Task ResetAsync(int leagueId)
    {
        await using var db = _fixture.OpenDb();
        db.NflPicks.RemoveRange(await db.NflPicks.Where(p => p.UserId == TestUserId).ToListAsync());
        db.LeagueInfo.RemoveRange(await db.LeagueInfo.Where(l => l.Id == leagueId).ToListAsync());
        db.Users.RemoveRange(await db.Users.Where(u => u.Id == TestUserId).ToListAsync());
        await db.SaveChangesAsync();
    }

    private async Task<int> SeedLeagueAndUserAsync()
    {
        await using var db = _fixture.OpenDb();
        db.Users.Add(new ApplicationUser { Id = TestUserId, UserName = "concurrency-tester", Email = "concurrency@demo.local" });
        var league = new LeagueInfo { LeagueName = $"Concurrency Test League {Guid.NewGuid()}", OwnerUserId = TestUserId };
        db.LeagueInfo.Add(league);
        await db.SaveChangesAsync();
        return league.Id;
    }

    /// <summary>
    /// The core regression this bead exists to prevent: without PickConcurrencyGuard's advisory
    /// lock, N concurrent TryAddNflPicksAsync calls each read the same pre-write pick count and
    /// can all pass the "room for one more" check before any of them writes — oversubscribing the
    /// 4-pick cap. With the lock, writers are serialized, so exactly RequiredPicks of them succeed
    /// no matter how many fire at once.
    /// </summary>
    [Fact]
    public async Task TryAddNflPicksAsync_UnderConcurrentRequests_NeverExceedsCap()
    {
        var leagueId = await SeedLeagueAndUserAsync();
        try
        {
            var factory = BuildFactory();
            var repo = new LeagueRepository(factory);

            // 8 concurrent single-pick submissions (distinct teams, so dedup logic never merges
            // them) racing for only 4 slots.
            var teams = new[] { "AAA", "BBB", "CCC", "DDD", "EEE", "FFF", "GGG", "HHH" };
            var tasks = teams.Select(team => repo.TryAddNflPicksAsync(
                [new NflPicks { LeagueId = leagueId, UserId = TestUserId, Team = team, Pick = PickType.Spread, NflWeek = Week, Season = Season }],
                TestUserId, leagueId, Season, Week, RequiredPicks));

            var results = await Task.WhenAll(tasks);

            Assert.Equal(RequiredPicks, results.Count(r => r));

            await using var db = _fixture.OpenDb();
            var actualCount = await db.NflPicks.CountAsync(p =>
                p.UserId == TestUserId && p.LeagueId == leagueId && p.Season == Season && p.NflWeek == Week);
            Assert.Equal(RequiredPicks, actualCount);
        }
        finally
        {
            await ResetAsync(leagueId);
        }
    }

    /// <summary>
    /// A remove racing against adds for the same (user, league, season, week) shares the same
    /// lock key as TryAddNflPicksAsync, so it can't interleave with an add in a way that lets a
    /// 5th pick through — e.g. remove-then-add "swap" attempts racing a separate add.
    /// </summary>
    [Fact]
    public async Task TryRemoveNflPickAsync_ConcurrentWithAdds_NeverLeavesCapExceeded()
    {
        var leagueId = await SeedLeagueAndUserAsync();
        try
        {
            var factory = BuildFactory();
            var repo = new LeagueRepository(factory);

            // Start at the cap (4 picks already in).
            await using (var seedDb = _fixture.OpenDb())
            {
                seedDb.NflPicks.AddRange(
                    new NflPicks { LeagueId = leagueId, UserId = TestUserId, Team = "AAA", Pick = PickType.Spread, NflWeek = Week, Season = Season },
                    new NflPicks { LeagueId = leagueId, UserId = TestUserId, Team = "BBB", Pick = PickType.Spread, NflWeek = Week, Season = Season },
                    new NflPicks { LeagueId = leagueId, UserId = TestUserId, Team = "CCC", Pick = PickType.Spread, NflWeek = Week, Season = Season },
                    new NflPicks { LeagueId = leagueId, UserId = TestUserId, Team = "DDD", Pick = PickType.Spread, NflWeek = Week, Season = Season });
                await seedDb.SaveChangesAsync();
            }

            // Race: one "swap" (remove AAA, add EEE) against a plain add of FFF that should never
            // find room. Regardless of interleaving, the final count must never exceed the cap.
            var removeTask = repo.TryRemoveNflPickAsync(TestUserId, leagueId, Season, Week, "AAA", PickType.Spread);
            var addAfterRemoveTask = repo.TryAddNflPicksAsync(
                [new NflPicks { LeagueId = leagueId, UserId = TestUserId, Team = "EEE", Pick = PickType.Spread, NflWeek = Week, Season = Season }],
                TestUserId, leagueId, Season, Week, RequiredPicks);
            var addWithNoRoomTask = repo.TryAddNflPicksAsync(
                [new NflPicks { LeagueId = leagueId, UserId = TestUserId, Team = "FFF", Pick = PickType.Spread, NflWeek = Week, Season = Season }],
                TestUserId, leagueId, Season, Week, RequiredPicks);

            await Task.WhenAll(removeTask, addAfterRemoveTask, addWithNoRoomTask);

            await using var db = _fixture.OpenDb();
            var actualCount = await db.NflPicks.CountAsync(p =>
                p.UserId == TestUserId && p.LeagueId == leagueId && p.Season == Season && p.NflWeek == Week);
            Assert.True(actualCount <= RequiredPicks, $"Expected at most {RequiredPicks} picks, found {actualCount}");
        }
        finally
        {
            await ResetAsync(leagueId);
        }
    }
}
