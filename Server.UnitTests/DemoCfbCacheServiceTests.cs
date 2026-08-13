using FourPlayWebApp.Server.Services;
using Xunit;

namespace FourPlayWebApp.Server.UnitTests;

/// <summary>
/// frizat-fe2: DemoCfbCacheService must serve real ESPN-shaped situation data for the seeded
/// CFB Championship game (event 401800011, IU vs MIA), the same way DemoEspnCacheService serves
/// sample_espn_nfl.json for NFL — not the always-null stub it was after CFB_DEMO_SITUATION was
/// ripped out in PR #163.
///
/// No IWebHostEnvironment/working-directory setup needed here (unlike before) — the fixture is
/// an embedded resource, not a loose file resolved via a relative path off ContentRootPath. That
/// prior approach only worked locally by coincidence and silently returned null on Railway,
/// where ContentRootPath points at the deployed app's own directory.
/// </summary>
public class DemoCfbCacheServiceTests
{
    [Fact]
    public async Task GetScoresAsync_ReturnsRealSituationData_ForChampionshipGame()
    {
        var service = new DemoCfbCacheService();

        var scores = await service.GetScoresAsync();

        var competition = Assert.Single(scores!.Events!).Competitions[0];
        Assert.NotNull(competition.Situation);
        Assert.Equal(2, competition.Situation.Down);
        Assert.Equal(7, competition.Situation.Distance);
        Assert.Equal(35, competition.Situation.YardLine);
        Assert.Equal("2nd & 7 at IU 35", competition.Situation.DownDistanceText);
    }

    [Fact]
    public async Task GetScoresAsync_ReturnsRealScoreAndTeams_ForChampionshipGame()
    {
        var service = new DemoCfbCacheService();

        var scores = await service.GetScoresAsync();

        var competition = Assert.Single(scores!.Events!).Competitions[0];
        Assert.Equal("401800011", competition.Id);
        var home = Assert.Single(competition.Competitors, c => c.HomeAway == Shared.Models.HomeAway.Home);
        var away = Assert.Single(competition.Competitors, c => c.HomeAway == Shared.Models.HomeAway.Away);
        Assert.Equal("IU", home.Team.Abbreviation);
        Assert.Equal(14, home.Score);
        Assert.Equal("MIA", away.Team.Abbreviation);
        Assert.Equal(7, away.Score);
        Assert.Equal(Shared.Models.TypeName.StatusInProgress, competition.Status.Type.Name);
    }
}
