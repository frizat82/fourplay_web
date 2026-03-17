using FourPlayWebApp.Shared.Models;
using FourPlayWebApp.Shared.Models.Enum;
using Refit;

namespace FourPlayWebApp.Shared.Refit;
public interface IEspnApi
{
    [Get("/api/espn/scores/week/{week}/{year}")]
    Task<EspnScores?> GetWeekScores(int week, int year, bool postSeason = false);
    
    [Get("/api/espn/scores")]
    Task<EspnScores?> GetScores();

    /*
    [Get("/api/espn/odds/events/{eventId}")]
    Task<ESPNCoreOddsApiResponse?> GetEventsWithOdds(int eventId);

    [Get("/api/espn/odds/events/{eventId}/provider/{provider}")]
    Task<ESPNCoreOddsApiResponse?> GetEventsWithOdds(int eventId, EspnOddsProviders provider);
    */
}
