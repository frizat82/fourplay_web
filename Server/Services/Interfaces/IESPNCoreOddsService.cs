using FourPlayWebApp.Shared.Models;

namespace FourPlayWebApp.Server.Services.Interfaces;

public interface IEspnCoreOddsService
{
    Task<EspnCoreOddsApiResponse?> GetEventsWithOddsAsync(int eventId);
    Task<EspnCoreOddsItem?> GetEventsWithOddsAsync(int eventId, int providerId);
}
