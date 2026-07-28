namespace FourPlayWebApp.Shared.Models.Data.Dtos;

public class NflScoreDto {
    public int            Id             { get; set; }
    public int            Season         { get; set; }
    public int            NflWeek        { get; set; }
    public string         HomeTeam       { get; set; } = string.Empty;
    public string         AwayTeam       { get; set; } = string.Empty;
    public int            HomeTeamScore  { get; set; }
    public int            AwayTeamScore  { get; set; }
    public DateTimeOffset GameTime       { get; set; }
}
