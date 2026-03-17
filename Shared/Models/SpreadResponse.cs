namespace FourPlayWebApp.Shared.Models;

public class SpreadResponse
{
    public string Team { get; set; } = string.Empty;
    public double? Spread { get; set; }
    public double? Over { get; set; }
    public double? Under { get; set; }
}
