using FourPlayWebApp.Server.Services;
using FourPlayWebApp.Shared.Models;

namespace FourPlayWebApp.Server.UnitTests;

/// <summary>
/// FinalScoresEspnMapper builds an EspnScores wire-shape response from this app's own persisted,
/// already-FINAL score rows (NflScores/CfbScores) — one shared implementation both sports' DB-first
/// "serve historical weeks without hitting ESPN" paths call, instead of each hand-rolling the
/// mapping. Extracted from what was previously a private, NFL-only helper in
/// DemoEspnCacheService.BuildEvent.
/// </summary>
public class FinalScoresEspnMapperTests {
    [Fact]
    public void Build_MapsHomeAndAwayCompetitorsWithCorrectAbbreviationsAndScores() {
        var games = new[] {
            new FinalScoresEspnMapper.FinishedGame("1", "KC", "DEN", 27, 20, new DateTimeOffset(2026, 1, 10, 18, 0, 0, TimeSpan.Zero)),
        };

        var result = FinalScoresEspnMapper.Build(games, season: 2025, week: 19, postSeason: true);

        var comp = result.Events!.Single().Competitions[0];
        var home = comp.Competitors.Single(c => c.HomeAway == HomeAway.Home);
        var away = comp.Competitors.Single(c => c.HomeAway == HomeAway.Away);
        Assert.Equal("KC", home.Team.Abbreviation);
        Assert.Equal(27, home.Score);
        Assert.Equal("DEN", away.Team.Abbreviation);
        Assert.Equal(20, away.Score);
    }

    [Fact]
    public void Build_MarksEveryGameAsFinal_SincePersistedRowsAreAlwaysAlreadyDecided() {
        var games = new[] {
            new FinalScoresEspnMapper.FinishedGame("1", "BUF", "MIA", 31, 17, DateTimeOffset.UtcNow),
        };

        var result = FinalScoresEspnMapper.Build(games, season: 2025, week: 1, postSeason: false);

        var status = result.Events!.Single().Competitions[0].Status;
        Assert.Equal(TypeName.StatusFinal, status.Type.Name);
        Assert.Equal(State.Post, status.Type.State);
        Assert.True(status.Type.Completed);
    }

    [Fact]
    public void Build_SetsSeasonWeekAndPostSeasonType_OnTheResultAndEachEvent() {
        var games = new[] { new FinalScoresEspnMapper.FinishedGame("1", "KC", "DEN", 1, 0, DateTimeOffset.UtcNow) };

        var result = FinalScoresEspnMapper.Build(games, season: 2025, week: 5, postSeason: true);

        Assert.Equal(2025, result.Season!.Year);
        Assert.Equal(3, result.Season!.Type); // postseason = 3, matches DemoEspnCacheService's convention
        Assert.Equal(5, result.Week!.Number);
        Assert.Equal(2025, result.Events!.Single().Season.Year);
        Assert.Equal(5, result.Events!.Single().Week.Number);
    }

    [Fact]
    public void Build_ReturnsOneEventPerGame_ForMultipleGames() {
        var games = new[] {
            new FinalScoresEspnMapper.FinishedGame("1", "KC", "DEN", 27, 20, DateTimeOffset.UtcNow),
            new FinalScoresEspnMapper.FinishedGame("2", "BUF", "MIA", 31, 17, DateTimeOffset.UtcNow),
        };

        var result = FinalScoresEspnMapper.Build(games, season: 2025, week: 19, postSeason: true);

        Assert.Equal(2, result.Events!.Length);
    }

    [Fact]
    public void Build_OmitsWeather_WhenNotProvided() {
        var games = new[] { new FinalScoresEspnMapper.FinishedGame("1", "KC", "DEN", 1, 0, DateTimeOffset.UtcNow) };

        var result = FinalScoresEspnMapper.Build(games, season: 2025, week: 1, postSeason: false);

        Assert.Null(result.Events!.Single().Weather);
    }

    [Fact]
    public void Build_IncludesWeather_WhenProvided() {
        var games = new[] {
            new FinalScoresEspnMapper.FinishedGame(
                "1", "KC", "DEN", 1, 0, DateTimeOffset.UtcNow,
                Weather: new FinalScoresEspnMapper.WeatherInfo("Sunny", "1", 72)),
        };

        var result = FinalScoresEspnMapper.Build(games, season: 2025, week: 1, postSeason: false);

        var weather = result.Events!.Single().Weather;
        Assert.NotNull(weather);
        Assert.Equal("Sunny", weather!.DisplayValue);
        Assert.Equal("1", weather.ConditionId);
        Assert.Equal(72, weather.Temperature);
    }
}
