using FourPlayWebApp.Shared.Models.Data;
using FourPlayWebApp.Shared.Models.Data.Dtos;

namespace FourPlayWebApp.Server.Services.Repositories.Interfaces;

public interface ICfbPicksRepository {
    Task<IEnumerable<CfbPicks>> GetUserPicksAsync(int leagueId, int cfbSlateId, string userId);
    Task<IEnumerable<CfbPickDto>> GetAllPicksForSlateAsync(int leagueId, int cfbSlateId);
    Task AddPicksAsync(IEnumerable<CfbPicks> picks);
    Task DeletePicksAsync(int leagueId, int cfbSlateId, string userId);
}
