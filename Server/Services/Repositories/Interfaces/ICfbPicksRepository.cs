using FourPlayWebApp.Shared.Models.Data;
using FourPlayWebApp.Shared.Models.Data.Dtos;
using FourPlayWebApp.Shared.Models.Enum;

namespace FourPlayWebApp.Server.Services.Repositories.Interfaces;

public interface ICfbPicksRepository {
    Task<IEnumerable<CfbPicks>> GetUserPicksAsync(int leagueId, int cfbSlateId, string userId);
    Task<IEnumerable<CfbPickDto>> GetAllPicksForSlateAsync(int leagueId, int cfbSlateId);
    Task AddPicksAsync(IEnumerable<CfbPicks> picks);
    Task DeletePicksAsync(int leagueId, int cfbSlateId, string userId);

    // Mirrors ILeagueRepository.TryAddNflPicksAsync — atomic cap-check-and-insert under an
    // advisory lock scoped to (userId, leagueId, season, cfbSlateId). See PickConcurrencyGuard.
    Task<bool> TryAddPicksAsync(IEnumerable<CfbPicks> newPicks, string userId, int leagueId, int season, int cfbSlateId, int requiredPicks);

    // Mirrors ILeagueRepository.TryRemoveNflPickAsync — idempotent single-pick removal under the
    // same advisory lock TryAddPicksAsync uses.
    Task<bool> TryRemovePickAsync(string userId, int leagueId, int season, int cfbSlateId, string team, PickType pickType);
}
