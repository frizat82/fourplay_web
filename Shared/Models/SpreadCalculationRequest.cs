namespace FourPlayWebApp.Shared.Models;

public class SpreadCalculationRequest : SpreadRequest
{
    public int PickTeamScore { get; set; }
    public int OtherTeamScore { get; set; }
}
