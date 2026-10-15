using FourPlayWebApp.Server.Models.Data;
using FourPlayWebApp.Server.Services.Repositories.Interfaces;
using FourPlayWebApp.Shared.Models.Enum;
using NSubstitute;

namespace FourPlayWebApp.Server.UnitTests;

/// <summary>
/// A season's NflSeasonWeekConfigs rows as the control table holds them (frizat-d47): each week's
/// pick count and postseason flag per /pick-rules — 1-18 = 4, Wild Card/Divisional = 3,
/// Conference = 2, Super Bowl = 1. For tests whose code under test reads the table
/// (SpreadCalculatorProvider, the NFL leaderboard).
/// </summary>
public static class TestWeekConfigs {
    public static List<NflSeasonWeekConfig> Nfl(int season) =>
        [.. Enumerable.Range(1, 22).Select(week => new NflSeasonWeekConfig {
            Season = season, WeekId = week, WeekLabel = $"Week {week}",
            WeekType = week > 18 ? WeekType.Playoff : WeekType.RegularSeason,
            IsPostSeason = week > 18,
            RequiredPicks = week switch { <= 18 => 4, 19 or 20 => 3, 21 => 2, _ => 1 },
            ScoringFormat = ScoringFormatFor(week switch { <= 18 => 4, 19 or 20 => 3, 21 => 2, _ => 1 }),
        })];

    /// <summary>The ScoringFormat the control tables' check constraint pairs with a pick count.</summary>
    public static ScoringFormat ScoringFormatFor(int requiredPicks) => requiredPicks switch {
        4 => ScoringFormat.Standard, 3 => ScoringFormat.Divisional, 2 => ScoringFormat.Conference, 1 => ScoringFormat.Final, _ => ScoringFormat.None,
    };

    /// <summary>A CFB slate's control-table pick count per /pick-rules: 1-14 = 4, 15-16 = 3, 17 = 2, 18 = 1.</summary>
    public static int CfbRequiredPicks(int slateNumber) => slateNumber switch { <= 14 => 4, 15 or 16 => 3, 17 => 2, _ => 1 };

    /// <summary>Stubs a mocked repository's season-scoped control-table read with <see cref="Nfl"/>.</summary>
    public static ILeagueRepository WithNflWeekConfigs(this ILeagueRepository repo, params int[] seasons) {
        foreach (var season in seasons)
            repo.GetNflSeasonWeekConfigsAsync(season).Returns(Nfl(season));
        return repo;
    }
}
