namespace FourPlayWebApp.Shared.Models.Data.Dtos;

public class NflSpreadDto {
    public int            Id              { get; set; }
    public int            Season          { get; set; }
    public int            NflWeek         { get; set; }
    public string         HomeTeam        { get; set; } = string.Empty;
    public string         AwayTeam        { get; set; } = string.Empty;
    public double         HomeTeamSpread  { get; set; }
    public double         AwayTeamSpread  { get; set; }
    public double         OverUnder       { get; set; }
    public DateTimeOffset GameTime        { get; set; }
}
