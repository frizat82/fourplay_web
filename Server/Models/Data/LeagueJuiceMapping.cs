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
    // Which week/slate this league's season effectively starts at — weeks before it require no
    // picks, don't appear on the leaderboard, and aren't billed WeeklyCost. Default 1 = no
    // exclusion (today's behavior for every existing league). Mainly for CFB leagues that want to
    // skip the often-lopsided week 1 buy games (frizat-o3x).
    public int StartWeek { get; set; } = 1;
    public DateTimeOffset DateCreated { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }
}
