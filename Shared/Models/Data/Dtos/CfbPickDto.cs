namespace FourPlayWebApp.Shared.Models.Data.Dtos;

public class CfbPickDto {
    public int Id { get; set; }
    public string UserId { get; set; } = string.Empty;
    public string UserName { get; set; } = string.Empty;
    public int LeagueId { get; set; }
    public int CfbSlateId { get; set; }
    public int EspnEventId { get; set; }
    public string Team { get; set; } = string.Empty;
    public string PickType { get; set; } = "Spread";
    public int Season { get; set; }
}
