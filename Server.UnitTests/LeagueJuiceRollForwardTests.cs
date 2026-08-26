using FourPlayWebApp.Server.Models.Data;
using FourPlayWebApp.Server.Services;

namespace FourPlayWebApp.Server.UnitTests;

// frizat-ugs: pure static helper shared by LeagueController.RollForwardJuice (manual "copy from
// prior season" button — requires an explicit prior season, errors otherwise) and the automatic
// LeagueJuiceLockJob (falls back to entity defaults when no prior season exists). No I/O, no
// mocks needed.
public class LeagueJuiceRollForwardTests
{
    private static LeagueJuiceMapping MakeMapping(int leagueId, int season, int juice = 13, int juiceDivisional = 10, int juiceConference = 6, int weeklyCost = 5) =>
        new() { LeagueId = leagueId, Season = season, Juice = juice, JuiceDivisional = juiceDivisional, JuiceConference = juiceConference, WeeklyCost = weeklyCost };

    // ── FindPriorSeasonMapping ─────────────────────────────────────────────────

    [Fact]
    public void FindPriorSeasonMapping_ReturnsMostRecentSeasonBeforeTarget()
    {
        var mappings = new[] { MakeMapping(1, 2023), MakeMapping(1, 2025), MakeMapping(1, 2024) };

        var result = LeagueJuiceRollForward.FindPriorSeasonMapping(2026, mappings);

        Assert.Equal(2025, result?.Season);
    }

    [Fact]
    public void FindPriorSeasonMapping_ReturnsNull_WhenNoSeasonBeforeTargetExists()
    {
        var mappings = new[] { MakeMapping(1, 2026), MakeMapping(1, 2027) };

        var result = LeagueJuiceRollForward.FindPriorSeasonMapping(2026, mappings);

        Assert.Null(result);
    }

    [Fact]
    public void FindPriorSeasonMapping_ReturnsNull_WhenNoMappingsExistAtAll()
    {
        var result = LeagueJuiceRollForward.FindPriorSeasonMapping(2026, []);

        Assert.Null(result);
    }

    // ── BuildMapping ───────────────────────────────────────────────────────────

    [Fact]
    public void BuildMapping_CopiesAllFourValuesFromPriorSeason_WhenGiven()
    {
        var prior = MakeMapping(1, 2025, juice: 20, juiceDivisional: 15, juiceConference: 8, weeklyCost: 12);

        var result = LeagueJuiceRollForward.BuildMapping(1, 2026, prior);

        Assert.Equal(1, result.LeagueId);
        Assert.Equal(2026, result.Season);
        Assert.Equal(20, result.Juice);
        Assert.Equal(15, result.JuiceDivisional);
        Assert.Equal(8, result.JuiceConference);
        Assert.Equal(12, result.WeeklyCost);
    }

    [Fact]
    public void BuildMapping_FallsBackToEntityDefaults_WhenNoPriorSeasonGiven()
    {
        // Deliberately NOT re-hardcoding 13/10/6/5 here — asserting against the entity's own
        // defaults (new LeagueJuiceMapping()) so this test can't silently drift from them.
        var expectedDefaults = new LeagueJuiceMapping();

        var result = LeagueJuiceRollForward.BuildMapping(1, 2026, copyFrom: null);

        Assert.Equal(1, result.LeagueId);
        Assert.Equal(2026, result.Season);
        Assert.Equal(expectedDefaults.Juice, result.Juice);
        Assert.Equal(expectedDefaults.JuiceDivisional, result.JuiceDivisional);
        Assert.Equal(expectedDefaults.JuiceConference, result.JuiceConference);
        Assert.Equal(expectedDefaults.WeeklyCost, result.WeeklyCost);
    }
}
