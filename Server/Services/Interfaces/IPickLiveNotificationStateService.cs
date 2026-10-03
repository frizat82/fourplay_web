using FourPlayWebApp.Server.Models.Data;
using FourPlayWebApp.Shared.Models.Enum;

namespace FourPlayWebApp.Server.Services.Interfaces;

public interface IPickLiveNotificationStateService
{
    /// <summary>Existing state rows for the given picks, keyed by PickId. A pick with no row yet
    /// (never recomputed before) is simply absent from the result.</summary>
    Task<Dictionary<int, PickLiveNotificationState>> GetStatesAsync(LeagueType sport, IReadOnlyCollection<int> pickIds);

    /// <summary>One batched upsert per recompute cycle, not one write per pick.</summary>
    Task UpsertStatesAsync(IReadOnlyCollection<PickLiveNotificationState> states);
}
