namespace FourPlayWebApp.Shared.Models;

public class BatchSpreadResponse
{
    public Dictionary<string, SpreadResponse> Responses { get; set; } = [];
}
