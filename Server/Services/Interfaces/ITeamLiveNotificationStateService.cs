using FourPlayWebApp.Server.Models.Data;
using FourPlayWebApp.Shared.Models.Enum;

namespace FourPlayWebApp.Server.Services.Interfaces;

public interface ITeamLiveNotificationStateService
{
    /// <summary>Existing state rows for the given (team, pickType) bets in one league/period, keyed
    /// by (Team, PickType). A bet with no row yet (never recomputed before) is simply absent.</summary>
    Task<Dictionary<(string Team, PickType PickType), TeamLiveNotificationState>> GetStatesAsync(
        LeagueType sport, int leagueId, int period, IReadOnlyCollection<(string Team, PickType PickType)> bets);

    /// <summary>One batched upsert per recompute cycle, not one write per bet.</summary>
    Task UpsertStatesAsync(IReadOnlyCollection<TeamLiveNotificationState> states);
}
