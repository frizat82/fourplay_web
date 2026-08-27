using FourPlayWebApp.Server.Models.Data;
using FourPlayWebApp.Server.Services;
using FourPlayWebApp.Server.Services.Repositories.Interfaces;
using NSubstitute;
using Xunit;

namespace FourPlayWebApp.Server.UnitTests;

// Direct unit tests for CfbCurrentSlateService — written while replacing its hardcoded
// `ConfiguredSeason = 2026` constant + manual "season - 1" fallback with the shared
// SeasonWindowResolver (frizat plan: wobbly-chasing-lynx). The season-2030 case below is
// the actual regression test for the bug: the old hardcoded-year implementation could
// never resolve any season other than 2026/2025 without a code change.
public class CfbCurrentSlateServiceTests
{
    private static CfbSlates Slate(int season, int slateNumber, DateOnly start, DateOnly end) => new() {
        Id = season * 100 + slateNumber,
        Season = season,
        SlateNumber = slateNumber,
        Label = $"Slate {slateNumber}",
        SlateType = "RegularSeason",
        StartDate = start,
        EndDate = end,
    };

    private static CfbCurrentSlateService BuildService(List<CfbSlates> slates, CfbSeasonWeekConfig? matchingConfig = null) {
        var repo = Substitute.For<ICfbRepository>();
        repo.GetAllSlatesAsync().Returns(slates);
        // One config per slate (across all seasons), so whichever slate the resolver picks as
        // "active" always has a matching CfbSeasonWeekConfig row — not just the first slate.
        // GetAllWeekConfigsAsync (not the per-season method) is what the service calls now.
        var allConfigs = slates.Select(s =>
            matchingConfig is not null && matchingConfig.Season == s.Season && matchingConfig.IvLeagueWeekNumber == s.SlateNumber
                ? matchingConfig
                : new CfbSeasonWeekConfig {
                    Season = s.Season,
                    IvLeagueWeekNumber = s.SlateNumber,
                    InScopeIvLeague = true,
                    SpreadLockDatetime = DateTime.UtcNow,
                }).ToList();
        repo.GetAllWeekConfigsAsync().Returns(allConfigs);
        return new CfbCurrentSlateService(repo);
    }

    [Fact]
    public async Task GetCurrentSlateAsync_ResolvesFromAnySeasonInTheDb_NoHardcodedYear() {
        // Regression test for the hardcoded-ConfiguredSeason bug: season 2030 has no
        // special-casing anywhere in the service — it must resolve purely from what's in
        // the DB.
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var config = new CfbSeasonWeekConfig { Season = 2030, IvLeagueWeekNumber = 1, InScopeIvLeague = true, SpreadLockDatetime = DateTime.UtcNow };
        var svc = BuildService([
            Slate(2030, 1, today.AddDays(-3), today.AddDays(4)),
        ], config);

        var result = await svc.GetCurrentSlateAsync();

        Assert.NotNull(result);
        Assert.Equal(2030, result.Season);
        Assert.Equal(1, result.SlateNumber);
    }

    [Fact]
    public async Task GetCurrentSlateAsync_ReturnsActiveSlate_WhenTodayIsWithinOne() {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var svc = BuildService([
            Slate(2026, 1, today.AddDays(-10), today.AddDays(-4)),
            Slate(2026, 2, today.AddDays(-3), today.AddDays(4)), // active
        ]);

        var result = await svc.GetCurrentSlateAsync();

        Assert.Equal(2, result!.SlateNumber);
    }

    [Fact]
    public async Task GetCurrentSlateAsync_ReturnsMostRecentlyCompleted_OffSeason() {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var svc = BuildService([
            Slate(2025, 3, today.AddDays(-60), today.AddDays(-53)),
            Slate(2025, 4, today.AddDays(-30), today.AddDays(-23)), // most recently completed
        ]);

        var result = await svc.GetCurrentSlateAsync();

        Assert.Equal(2025, result!.Season);
        Assert.Equal(4, result.SlateNumber);
    }

    // frizat-9xg: a slate's own SpreadLockDatetime (from its matching CfbSeasonWeekConfig row)
    // — not its calendar StartDate/EndDate — is what the resolver actually keys off. These two
    // build the repo mock directly (not via BuildService's shared per-season config, which
    // can't give two slates in the same season independently controlled spread-lock times).
    [Fact]
    public async Task GetCurrentSlateAsync_StaysOnPreviousSlate_WhenNextSlatesSpreadLockIsMoreThanTwoDaysAway() {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var now = DateTime.UtcNow;
        var slate3 = Slate(2026, 3, today.AddDays(-10), today.AddDays(-3));
        var slate4 = Slate(2026, 4, today.AddDays(-3), today.AddDays(4)); // calendar-active right now
        var repo = Substitute.For<ICfbRepository>();
        repo.GetAllSlatesAsync().Returns(new List<CfbSlates> { slate3, slate4 });
        repo.GetAllWeekConfigsAsync().Returns(new List<CfbSeasonWeekConfig> {
            new() { Season = 2026, IvLeagueWeekNumber = 3, InScopeIvLeague = true, SpreadLockDatetime = now.AddDays(-9) }, // already started
            new() { Season = 2026, IvLeagueWeekNumber = 4, InScopeIvLeague = true, SpreadLockDatetime = now.AddDays(5) },  // 5 days out
        });

        var result = await new CfbCurrentSlateService(repo).GetCurrentSlateAsync();

        Assert.Equal(3, result!.SlateNumber);
    }

    [Fact]
    public async Task GetCurrentSlateAsync_EarlyActivatesNextSlate_WhenWithinTwoDaysOfItsSpreadLock() {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var now = DateTime.UtcNow;
        var slate3 = Slate(2026, 3, today.AddDays(-10), today.AddDays(-3));
        var slate4 = Slate(2026, 4, today.AddDays(3), today.AddDays(10)); // hasn't calendar-started yet
        var repo = Substitute.For<ICfbRepository>();
        repo.GetAllSlatesAsync().Returns(new List<CfbSlates> { slate3, slate4 });
        repo.GetAllWeekConfigsAsync().Returns(new List<CfbSeasonWeekConfig> {
            new() { Season = 2026, IvLeagueWeekNumber = 3, InScopeIvLeague = true, SpreadLockDatetime = now.AddDays(-9) },
            new() { Season = 2026, IvLeagueWeekNumber = 4, InScopeIvLeague = true, SpreadLockDatetime = now.AddDays(1) }, // only 1 day out
        });

        var result = await new CfbCurrentSlateService(repo).GetCurrentSlateAsync();

        Assert.Equal(4, result!.SlateNumber);
    }

    // frizat-9xg /code-review: DemoDataSeeder legitimately seeds multiple rows with
    // IvLeagueWeekNumber=99 in the same season (bye/dead weeks excluded from IV League scope —
    // CfbSeasonWeekConfigConfiguration's unique index is filtered specifically to allow this).
    // A naive Dictionary keyed on (Season, IvLeagueWeekNumber) with no filter throws
    // "An item with the same key has already been added" the moment a season has 2+ such rows.
    [Fact]
    public async Task GetCurrentSlateAsync_DoesNotThrow_WhenSeasonHasMultipleOutOfScopeWeek99Rows() {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var slate = Slate(2026, 1, today.AddDays(-1), today.AddDays(1));
        var repo = Substitute.For<ICfbRepository>();
        repo.GetAllSlatesAsync().Returns(new List<CfbSlates> { slate });
        repo.GetAllWeekConfigsAsync().Returns(new List<CfbSeasonWeekConfig> {
            new() { Season = 2026, IvLeagueWeekNumber = 1, InScopeIvLeague = true, SpreadLockDatetime = DateTime.UtcNow.AddDays(-1) },
            new() { Season = 2026, IvLeagueWeekNumber = 99, InScopeIvLeague = false, SpreadLockDatetime = DateTime.UtcNow }, // e.g. Army-Navy
            new() { Season = 2026, IvLeagueWeekNumber = 99, InScopeIvLeague = false, SpreadLockDatetime = DateTime.UtcNow }, // e.g. bowl dead week
        });

        var result = await new CfbCurrentSlateService(repo).GetCurrentSlateAsync();

        Assert.Equal(1, result!.SlateNumber);
    }

    [Fact]
    public async Task GetCurrentSlateAsync_ReturnsNull_WhenNoSlatesExistAtAll() {
        var svc = BuildService([]);

        var result = await svc.GetCurrentSlateAsync();

        Assert.Null(result);
    }

    [Fact]
    public async Task GetCurrentSlateAsync_Throws_WhenResolvedSlateHasNoMatchingWeekConfig() {
        // Existing data-integrity invariant (unchanged by this refactor): every CfbSlates
        // row must have a matching CfbSeasonWeekConfig row.
        var repo = Substitute.For<ICfbRepository>();
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var slate = Slate(2026, 1, today.AddDays(-1), today.AddDays(1));
        repo.GetAllSlatesAsync().Returns(new List<CfbSlates> { slate });
        repo.GetAllWeekConfigsAsync().Returns(new List<CfbSeasonWeekConfig>());
        var svc = new CfbCurrentSlateService(repo);

        await Assert.ThrowsAsync<InvalidOperationException>(() => svc.GetCurrentSlateAsync());
    }

    [Fact]
    public async Task IsSeasonActiveAsync_True_WhenTodayIsWithinASlateWindow() {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var svc = BuildService([Slate(2026, 1, today.AddDays(-3), today.AddDays(4))]);

        Assert.True(await svc.IsSeasonActiveAsync());
    }

    [Fact]
    public async Task IsSeasonActiveAsync_False_OffSeason() {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var svc = BuildService([Slate(2025, 4, today.AddDays(-60), today.AddDays(-53))]);

        Assert.False(await svc.IsSeasonActiveAsync());
    }
}
