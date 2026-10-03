using FourPlayWebApp.Server.Data;
using FourPlayWebApp.Server.Models.Data;
using FourPlayWebApp.Server.Services.Interfaces;
using FourPlayWebApp.Shared.Models.Enum;
using Microsoft.EntityFrameworkCore;

namespace FourPlayWebApp.Server.Services;

public class TeamLiveNotificationStateService(IDbContextFactory<ApplicationDbContext> dbContextFactory)
    : ITeamLiveNotificationStateService
{
    public async Task<Dictionary<(string Team, PickType PickType), TeamLiveNotificationState>> GetStatesAsync(
        LeagueType sport, int leagueId, int period, IReadOnlyCollection<(string Team, PickType PickType)> bets)
    {
        if (bets.Count == 0) return [];
        // EF can't translate a tuple .Contains, but it can translate one over the team names alone
        // — narrows the row count fetched to just these teams instead of every bet ever notified
        // for this league/period; the PickType half of the match still happens in memory below.
        var teams = bets.Select(b => b.Team).Distinct().ToList();
        await using var db = await dbContextFactory.CreateDbContextAsync();
        var rows = await db.TeamLiveNotificationStates.AsNoTracking()
            .Where(s => s.Sport == sport && s.LeagueId == leagueId && s.Period == period && teams.Contains(s.Team))
            .ToListAsync();
        return rows.Where(r => bets.Contains((r.Team, r.PickType))).ToDictionary(r => (r.Team, r.PickType));
    }

    public async Task UpsertStatesAsync(IReadOnlyCollection<TeamLiveNotificationState> states)
    {
        if (states.Count == 0) return;
        await using var db = await dbContextFactory.CreateDbContextAsync();

        foreach (var byScope in states.GroupBy(s => (s.Sport, s.LeagueId, s.Period)))
        {
            var existing = await db.TeamLiveNotificationStates
                .Where(s => s.Sport == byScope.Key.Sport && s.LeagueId == byScope.Key.LeagueId && s.Period == byScope.Key.Period)
                .ToListAsync();
            var existingByKey = existing.ToDictionary(s => (s.Team, s.PickType));

            foreach (var state in byScope)
            {
                if (existingByKey.TryGetValue((state.Team, state.PickType), out var row))
                {
                    row.LastNotifiedCovering = state.LastNotifiedCovering;
                    row.LastNotifiedAt = state.LastNotifiedAt;
                    row.FinalNotifiedAt = state.FinalNotifiedAt;
                    row.UpdatedAt = DateTimeOffset.UtcNow;
                }
                else
                {
                    state.UpdatedAt = DateTimeOffset.UtcNow;
                    db.TeamLiveNotificationStates.Add(state);
                }
            }
        }

        await db.SaveChangesAsync();
    }
}
