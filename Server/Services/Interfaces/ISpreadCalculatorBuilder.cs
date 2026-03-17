namespace FourPlayWebApp.Server.Services.Interfaces;

public interface ISpreadCalculatorBuilder
{
    ISpreadCalculatorBuilder WithLeagueId(int leagueId);
    ISpreadCalculatorBuilder WithWeek(int week);
    ISpreadCalculatorBuilder WithSeason(int season);
    Task<ISpreadCalculator> BuildAsync();
}