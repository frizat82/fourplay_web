using FourPlayWebApp.Server.Services;
using FourPlayWebApp.Server.Services.Repositories.Interfaces;
using FourPlayWebApp.Shared.Models.Data;
using NSubstitute;

namespace FourPlayWebApp.Server.UnitTests;

public class GameScheduleServiceTests
{
    private readonly ILeagueRepository _repo;
    private readonly GameScheduleService _service;

    private const int Season = 2024;
    private const int Week = 5;

    public GameScheduleServiceTests()
    {
        _repo = Substitute.For<ILeagueRepository>();
        _service = new GameScheduleService(_repo);
    }

    private static NflSpreads BuildSpread(string home, string away, DateTime kickoff) => new()
    {
        Season = Season,
        NflWeek = Week,
        HomeTeam = home,
        AwayTeam = away,
        GameTime = kickoff,
    };

    // -------------------------------------------------------------------------
    // GetGamesThisWeekAsync
    // -------------------------------------------------------------------------

    [Fact]
    public async Task GetGamesThisWeekAsync_ReturnsAllGamesForWeek()
    {
        var sunday = new DateTime(2024, 10, 13, 18, 0, 0, DateTimeKind.Utc);
        var spreads = new List<NflSpreads>
        {
            BuildSpread("Chiefs", "Raiders", sunday),
            BuildSpread("Eagles", "Giants", sunday.AddHours(3)),
        };
        _repo.GetNflSpreadsAsync(Season, Week).Returns(spreads);

        var result = await _service.GetGamesThisWeekAsync(Season, Week);

        Assert.Equal(2, result.Count);
        Assert.Contains(result, g => g.HomeTeam == "Chiefs" && g.AwayTeam == "Raiders");
        Assert.Contains(result, g => g.HomeTeam == "Eagles" && g.AwayTeam == "Giants");
    }

    [Fact]
    public async Task GetGamesThisWeekAsync_WhenSpreadsNull_ReturnsEmptyList()
    {
        _repo.GetNflSpreadsAsync(Season, Week).Returns((List<NflSpreads>?)null);

        var result = await _service.GetGamesThisWeekAsync(Season, Week);

        Assert.Empty(result);
    }

    // -------------------------------------------------------------------------
    // GetGameDaysThisWeekAsync
    // -------------------------------------------------------------------------

    [Fact]
    public async Task GetGameDaysThisWeekAsync_ReturnsDistinctDays()
    {
        var saturday = new DateTime(2024, 1, 13, 16, 0, 0, DateTimeKind.Utc);
        var sunday = new DateTime(2024, 1, 14, 18, 0, 0, DateTimeKind.Utc);
        var spreads = new List<NflSpreads>
        {
            BuildSpread("Chiefs", "Raiders", saturday),
            BuildSpread("Bills", "Steelers", saturday.AddHours(3)),  // same day, different time
            BuildSpread("Eagles", "Rams", sunday),
        };
        _repo.GetNflSpreadsAsync(Season, Week).Returns(spreads);

        var result = await _service.GetGameDaysThisWeekAsync(Season, Week);

        Assert.Equal(2, result.Count);
        Assert.Contains(DateOnly.FromDateTime(saturday), result);
        Assert.Contains(DateOnly.FromDateTime(sunday), result);
    }

    [Fact]
    public async Task GetGameDaysThisWeekAsync_ReturnsDaysInAscendingOrder()
    {
        var sunday = new DateTime(2024, 1, 14, 18, 0, 0, DateTimeKind.Utc);
        var saturday = new DateTime(2024, 1, 13, 16, 0, 0, DateTimeKind.Utc);
        var spreads = new List<NflSpreads>
        {
            BuildSpread("Eagles", "Rams", sunday),   // intentionally listed out of order
            BuildSpread("Chiefs", "Raiders", saturday),
        };
        _repo.GetNflSpreadsAsync(Season, Week).Returns(spreads);

        var result = await _service.GetGameDaysThisWeekAsync(Season, Week);

        Assert.Equal(DateOnly.FromDateTime(saturday), result[0]);
        Assert.Equal(DateOnly.FromDateTime(sunday), result[1]);
    }

    // -------------------------------------------------------------------------
    // GetFirstKickoffTodayAsync
    // -------------------------------------------------------------------------

    [Fact]
    public async Task GetFirstKickoffTodayAsync_ReturnsEarliestKickoff()
    {
        var todayUtc = DateTime.UtcNow.Date;
        var early = todayUtc.AddHours(17);
        var late = todayUtc.AddHours(20);
        var spreads = new List<NflSpreads>
        {
            BuildSpread("Chiefs", "Raiders", late),
            BuildSpread("Eagles", "Giants", early),
        };
        _repo.GetNflSpreadsAsync(Season, Week).Returns(spreads);

        var result = await _service.GetFirstKickoffTodayAsync(Season, Week);

        Assert.Equal(early, result);
    }

    [Fact]
    public async Task GetFirstKickoffTodayAsync_ReturnsNullWhenNoGamesToday()
    {
        var yesterday = DateTime.UtcNow.Date.AddDays(-1).AddHours(18);
        var spreads = new List<NflSpreads>
        {
            BuildSpread("Chiefs", "Raiders", yesterday),
        };
        _repo.GetNflSpreadsAsync(Season, Week).Returns(spreads);

        var result = await _service.GetFirstKickoffTodayAsync(Season, Week);

        Assert.Null(result);
    }

    [Fact]
    public async Task GetFirstKickoffTodayAsync_WhenSpreadsNull_ReturnsNull()
    {
        _repo.GetNflSpreadsAsync(Season, Week).Returns((List<NflSpreads>?)null);

        var result = await _service.GetFirstKickoffTodayAsync(Season, Week);

        Assert.Null(result);
    }

    // -------------------------------------------------------------------------
    // HasGamesTodayAsync
    // -------------------------------------------------------------------------

    [Fact]
    public async Task HasGamesTodayAsync_ReturnsTrueWhenGamesExist()
    {
        var todayGame = DateTime.UtcNow.Date.AddHours(18);
        _repo.GetNflSpreadsAsync(Season, Week).Returns(new List<NflSpreads>
        {
            BuildSpread("Chiefs", "Raiders", todayGame),
        });

        var result = await _service.HasGamesTodayAsync(Season, Week);

        Assert.True(result);
    }

    [Fact]
    public async Task HasGamesTodayAsync_ReturnsFalseWhenNoGames()
    {
        var tomorrowGame = DateTime.UtcNow.Date.AddDays(1).AddHours(18);
        _repo.GetNflSpreadsAsync(Season, Week).Returns(new List<NflSpreads>
        {
            BuildSpread("Chiefs", "Raiders", tomorrowGame),
        });

        var result = await _service.HasGamesTodayAsync(Season, Week);

        Assert.False(result);
    }

    [Fact]
    public async Task HasGamesTodayAsync_ReturnsFalseWhenSpreadsNull()
    {
        _repo.GetNflSpreadsAsync(Season, Week).Returns((List<NflSpreads>?)null);

        var result = await _service.HasGamesTodayAsync(Season, Week);

        Assert.False(result);
    }
}
