using FourPlayWebApp.Shared.Models;
using FourPlayWebApp.Shared.Models.Data.Dtos;
using FourPlayWebApp.Shared.Models.Enum;

namespace FourPlayWebApp.Server.UnitTests;

/// <summary>
/// Tests for LiveGameDto mapping from raw ESPN Competition data.
/// Covers frizat-0x4: clean provider-agnostic DTO for live game situation.
/// </summary>
public class LiveGameDtoTests
{
    // ── Helpers ──────────────────────────────────────────────────────────────

    private static Competition BuildCompetition(
        string homeId = "1", string homeAbbr = "KC",
        string awayId = "2", string awayAbbr = "BUF",
        string? possessionId = null,
        int yardLine = 40, int down = 1, int distance = 10,
        bool isRedZone = false,
        bool completed = false)
    {
        return new Competition
        {
            Date = DateTimeOffset.UtcNow,
            Competitors =
            [
                new Competitor { Id = homeId, HomeAway = HomeAway.Home, Team = new EspnTeam { Abbreviation = homeAbbr }, Score = 0, Records = [] },
                new Competitor { Id = awayId, HomeAway = HomeAway.Away, Team = new EspnTeam { Abbreviation = awayAbbr }, Score = 0, Records = [] },
            ],
            Status = new EspnStatus { Type = new StatusType { Completed = completed } },
            Odds = [],
            Situation = possessionId is null ? null : new EspnSitutation
            {
                Down = down,
                YardLine = yardLine,
                Distance = distance,
                IsRedZone = isRedZone,
                Possession = possessionId,
                DownDistanceText = $"{down}st & {distance}",
            }
        };
    }

    // ── HomeTeam / AwayTeam mapping ───────────────────────────────────────────

    [Fact]
    public void FromCompetition_MapsHomeAndAwayTeamAbbreviations()
    {
        var competition = BuildCompetition(homeAbbr: "KC", awayAbbr: "BUF");

        var dto = LiveGameDto.FromCompetition(competition);

        Assert.Equal("KC", dto.HomeTeam);
        Assert.Equal("BUF", dto.AwayTeam);
    }

    // ── Possession resolution ─────────────────────────────────────────────────

    [Fact]
    public void FromCompetition_ResolvesPossessionIdToAbbreviation_WhenAwayHasBall()
    {
        var competition = BuildCompetition(homeId: "1", awayId: "2", possessionId: "2", awayAbbr: "BUF");

        var dto = LiveGameDto.FromCompetition(competition);

        Assert.Equal("BUF", dto.Situation!.PossessionTeam);
    }

    [Fact]
    public void FromCompetition_ResolvesPossessionIdToAbbreviation_WhenHomeHasBall()
    {
        var competition = BuildCompetition(homeId: "1", awayId: "2", possessionId: "1", homeAbbr: "KC");

        var dto = LiveGameDto.FromCompetition(competition);

        Assert.Equal("KC", dto.Situation!.PossessionTeam);
    }

    // ── IsHomePossession ──────────────────────────────────────────────────────

    [Fact]
    public void FromCompetition_SetsIsHomePossession_True_WhenHomeCompetitorHasBall()
    {
        var competition = BuildCompetition(homeId: "1", awayId: "2", possessionId: "1");

        var dto = LiveGameDto.FromCompetition(competition);

        Assert.True(dto.Situation!.IsHomePossession);
    }

    [Fact]
    public void FromCompetition_SetsIsHomePossession_False_WhenAwayCompetitorHasBall()
    {
        var competition = BuildCompetition(homeId: "1", awayId: "2", possessionId: "2");

        var dto = LiveGameDto.FromCompetition(competition);

        Assert.False(dto.Situation!.IsHomePossession);
    }

    // ── Null situation ────────────────────────────────────────────────────────

    [Fact]
    public void FromCompetition_SituationIsNull_WhenCompetitionHasNoSituation()
    {
        var competition = BuildCompetition(possessionId: null);

        var dto = LiveGameDto.FromCompetition(competition);

        Assert.Null(dto.Situation);
    }

    // ── IsRedZone ─────────────────────────────────────────────────────────────

    [Fact]
    public void FromCompetition_IsRedZone_True_MapsCorrectly()
    {
        var competition = BuildCompetition(possessionId: "1", isRedZone: true);

        var dto = LiveGameDto.FromCompetition(competition);

        Assert.True(dto.Situation!.IsRedZone);
    }

    [Fact]
    public void FromCompetition_IsRedZone_False_MapsCorrectly()
    {
        var competition = BuildCompetition(possessionId: "1", isRedZone: false);

        var dto = LiveGameDto.FromCompetition(competition);

        Assert.False(dto.Situation!.IsRedZone);
    }

    // ── YardLine / Down / Distance ────────────────────────────────────────────

    [Fact]
    public void FromCompetition_MapsYardLineDownDistance()
    {
        var competition = BuildCompetition(possessionId: "1", yardLine: 75, down: 3, distance: 4);

        var dto = LiveGameDto.FromCompetition(competition);

        Assert.Equal(75, dto.Situation!.YardLine);
        Assert.Equal(3, dto.Situation.Down);
        Assert.Equal(4, dto.Situation.Distance);
    }
}
