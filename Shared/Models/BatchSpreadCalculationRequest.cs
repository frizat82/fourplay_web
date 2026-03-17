namespace FourPlayWebApp.Shared.Models;

public class BatchSpreadCalculationRequest
{
    public List<SpreadCalculationRequest> Calculations { get; set; } = [];
}