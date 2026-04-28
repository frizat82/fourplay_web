namespace FourPlayWebApp.Shared.Models;

public class CompetitionBySeason {
    public int Id { get; set; }
    public Season Season { get; set; }
    public Competition Competition { get; set; }
}
