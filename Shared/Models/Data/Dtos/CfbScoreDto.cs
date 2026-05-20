namespace FourPlayWebApp.Shared.Models.Data.Dtos;

public class CfbScoreDto {
    public int    Id             { get; set; }
    public int    CfbSlateId     { get; set; }
    public int    EspnEventId    { get; set; }
    public string HomeTeam       { get; set; } = string.Empty;
    public string AwayTeam       { get; set; } = string.Empty;
    public int    HomeTeamScore  { get; set; }
    public int    AwayTeamScore  { get; set; }
    public string GameStatus     { get; set; } = string.Empty;
    public string GameTime       { get; set; } = string.Empty;
    public string? WeatherDisplayValue { get; set; }
    public string? WeatherConditionId  { get; set; }
    public int?    WeatherTemperatureF  { get; set; }
}
