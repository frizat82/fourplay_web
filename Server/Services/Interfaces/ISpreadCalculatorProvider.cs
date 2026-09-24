namespace FourPlayWebApp.Server.Services.Interfaces;

/// <summary>
/// A league's teased NFL lines for one week. Stateless — every input is a parameter — so one instance
/// is safe to share across concurrent callers. (It replaced a fluent WithLeagueId/WithWeek/WithSeason
/// builder whose mutable fields raced when an instance was shared.)
/// </summary>
public interface ISpreadCalculatorProvider
{
    Task<ISpreadCalculator> GetForNflWeekAsync(int leagueId, int season, int week);
}
