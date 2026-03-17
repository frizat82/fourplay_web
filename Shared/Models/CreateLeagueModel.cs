namespace FourPlayWebApp.Shared.Models;
public class CreateLeagueModel {
    public string LeagueName { get; set; }
    public int Juice { get; set; }
    public int JuiceDivisional { get; set; }
    public int JuiceConference { get; set; }
    public int Season { get; set; }
    public int WeeklyCost { get; set; }
}