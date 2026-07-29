using FourPlayWebApp.Shared.Models.Data;
using FourPlayWebApp.Shared.Models.Data.Dtos;

namespace FourPlayWebApp.Server.Models.Mappers;

public static class NflMapper
{
    public static NflScoreDto ToDto(this NflScores s) => new()
    {
        Id = s.Id, Season = s.Season, NflWeek = s.NflWeek,
        HomeTeam = s.HomeTeam, AwayTeam = s.AwayTeam,
        HomeTeamScore = s.HomeTeamScore, AwayTeamScore = s.AwayTeamScore,
        GameTime = s.GameTime,
    };

    public static NflSpreadDto ToDto(this NflSpreads s) => new()
    {
        Id = s.Id, Season = s.Season, NflWeek = s.NflWeek,
        HomeTeam = s.HomeTeam, AwayTeam = s.AwayTeam,
        HomeTeamSpread = s.HomeTeamSpread, AwayTeamSpread = s.AwayTeamSpread,
        OverUnder = s.OverUnder, GameTime = s.GameTime,
    };
}
