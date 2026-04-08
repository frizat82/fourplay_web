namespace FourPlayWebApp.Shared.Models.Data.Dtos;

public class LeagueJuiceMappingDto
{
    public int Id { get; set; }
    public int LeagueId { get; set; }
    public string LeagueName { get; set; } = string.Empty;
    public int Season { get; set; }
    public int Juice { get; set; }
    public int JuiceDivisional { get; set; }
    public int JuiceConference { get; set; }
    public int WeeklyCost { get; set; }
    public DateTimeOffset DateCreated { get; set; } = DateTimeOffset.UtcNow;
}
