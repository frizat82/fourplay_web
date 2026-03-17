namespace FourPlayWebApp.Shared.Models;

public class BatchSpreadCalculationResponse
{
    public Dictionary<string, SpreadCalculationResponse> Results { get; set; } = [];
}