namespace FourPlayWebApp.Server.Services.Interfaces;

/// <summary>
/// A league's teased NFL lines for one week. Stateless — every input is a parameter — so one instance
/// is safe to share across concurrent callers.
/// </summary>
public interface ISpreadCalculatorProvider
{
    Task<ISpreadCalculator> GetForNflWeekAsync(int leagueId, int season, int week);
}
