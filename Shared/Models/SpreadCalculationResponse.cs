namespace FourPlayWebApp.Shared.Models;

public class SpreadCalculationResponse : SpreadResponse
{
    public bool IsWinner { get; set; }
    public bool IsOverWinner { get; set; }
    public bool IsUnderWinner { get; set; }
}
