using FourPlayWebApp.Shared.Models;
using FourPlayWebApp.Shared.Models.Enum;

namespace FourPlayWebApp.Shared.Models.Data.Dtos;

public class LiveGameDto
{
    public string HomeTeam { get; set; } = string.Empty;
    public string AwayTeam { get; set; } = string.Empty;
    public long HomeScore { get; set; }
    public long AwayScore { get; set; }
    public bool IsCompleted { get; set; }
    public DateTimeOffset KickoffUtc { get; set; }
    public GameSituationDto? Situation { get; set; }

    public static LiveGameDto FromCompetition(Competition competition)
    {
        var home = competition.Competitors.First(c => c.HomeAway == HomeAway.Home);
        var away = competition.Competitors.First(c => c.HomeAway == HomeAway.Away);

        return new LiveGameDto
        {
            HomeTeam = home.Team.Abbreviation,
            AwayTeam = away.Team.Abbreviation,
            HomeScore = home.Score,
            AwayScore = away.Score,
            IsCompleted = competition.Status?.Type?.Completed ?? false,
            KickoffUtc = competition.Date,
            Situation = competition.Situation is null
                ? null
                : GameSituationDto.FromSituation(competition.Situation, home, away),
        };
    }
}

public class GameSituationDto
{
    public string? PossessionTeam { get; set; }
    public bool IsHomePossession { get; set; }
    public int YardLine { get; set; }
    public int Down { get; set; }
    public int Distance { get; set; }
    public bool IsRedZone { get; set; }
    public string DownDistanceText { get; set; } = string.Empty;

    internal static GameSituationDto FromSituation(EspnSitutation situation, Competitor home, Competitor away)
    {
        var possessor = situation.Possession == home.Id ? home
                      : situation.Possession == away.Id ? away
                      : null;

        return new GameSituationDto
        {
            PossessionTeam = possessor?.Team.Abbreviation,
            IsHomePossession = possessor?.HomeAway == HomeAway.Home,
            YardLine = situation.YardLine,
            Down = situation.Down,
            Distance = situation.Distance,
            IsRedZone = situation.IsRedZone ?? false,
            DownDistanceText = situation.DownDistanceText ?? string.Empty,
        };
    }
}
