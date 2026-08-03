using System.Text.Json;
using FourPlayWebApp.Shared.Models;

namespace FourPlayWebApp.Server.UnitTests;

/// <summary>
/// frizat-703.5: sample_espn_nfl_halftime.json / sample_espn_nfl_in_progress.json are spliced from a
/// real captured ESPN game (ATL @ IND, real end-of-Q2 and mid-Q3 values from the play-by-play).
/// This is a plain offline deserialization check (no network) that runs in the default suite — it
/// exists so an accidental edit to either fixture file is caught immediately, not just when someone
/// happens to run the frontend tests that consume the same real values.
/// </summary>
public class EspnLiveStatusFixtureTests
{
    private static string RepoRoot()
    {
        var dir = AppContext.BaseDirectory;
        while (dir is not null && !File.Exists(Path.Combine(dir, "FourPlayWebApp.sln")))
            dir = Directory.GetParent(dir)?.FullName;
        return dir ?? throw new InvalidOperationException("Could not locate repo root from test base directory.");
    }

    private static EspnScores LoadFixture(string fileName)
    {
        var path = Path.Combine(RepoRoot(), fileName);
        var json = File.ReadAllText(path);
        return JsonSerializer.Deserialize<EspnScores>(json, EspnApiServiceJsonConverter.Settings)!;
    }

    [Fact]
    public void HalftimeFixture_DeserializesWithRealCapturedValues()
    {
        var scores = LoadFixture("sample_espn_nfl_halftime.json");
        var status = scores.Events!.Single().Competitions.Single().Status!;

        Assert.Equal(TypeName.StatusHalftime, status.Type!.Name);
        Assert.Equal(2, status.Period);
        Assert.Equal("0:00", status.DisplayClock);
    }

    [Fact]
    public void InProgressFixture_DeserializesWithRealCapturedValues()
    {
        var scores = LoadFixture("sample_espn_nfl_in_progress.json");
        var status = scores.Events!.Single().Competitions.Single().Status!;

        Assert.Equal(TypeName.StatusInProgress, status.Type!.Name);
        Assert.Equal(3, status.Period);
        Assert.Equal("9:47", status.DisplayClock);
    }
}
