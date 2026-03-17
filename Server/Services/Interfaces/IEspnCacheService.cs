using FourPlayWebApp.Shared.Models;

namespace FourPlayWebApp.Server.Services.Interfaces;
public interface IEspnCacheService
{
    Task<EspnScores?> GetScoresAsync();
}