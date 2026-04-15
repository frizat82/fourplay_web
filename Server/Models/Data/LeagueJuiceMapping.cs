namespace FourPlayWebApp.Server.Models.Data;
public class LeagueJuiceMapping {
    public int Id { get; set; }
    public LeagueInfo League { get; set; }
    public int LeagueId { get; set; }
    public int Season { get; set; }
    public int Juice { get; set; } = 13;
    public int JuiceDivisional { get; set; } = 10;
    public int JuiceConference { get; set; } = 6;
    public int WeeklyCost { get; set; } = 5;
    public DateTimeOffset DateCreated { get; set; }
}
