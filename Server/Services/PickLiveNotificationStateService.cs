using FourPlayWebApp.Server.Data;
using FourPlayWebApp.Server.Models.Data;
using FourPlayWebApp.Server.Services.Interfaces;
using FourPlayWebApp.Shared.Models.Enum;
using Microsoft.EntityFrameworkCore;

namespace FourPlayWebApp.Server.Services;

public class PickLiveNotificationStateService(IDbContextFactory<ApplicationDbContext> dbContextFactory)
    : IPickLiveNotificationStateService
{
    public async Task<Dictionary<int, PickLiveNotificationState>> GetStatesAsync(LeagueType sport, IReadOnlyCollection<int> pickIds)
    {
        if (pickIds.Count == 0) return [];
        await using var db = await dbContextFactory.CreateDbContextAsync();
        var rows = await db.PickLiveNotificationStates.AsNoTracking()
            .Where(s => s.Sport == sport && pickIds.Contains(s.PickId))
            .ToListAsync();
        return rows.ToDictionary(s => s.PickId);
    }

    public async Task UpsertStatesAsync(IReadOnlyCollection<PickLiveNotificationState> states)
    {
        if (states.Count == 0) return;
        await using var db = await dbContextFactory.CreateDbContextAsync();

        foreach (var bySport in states.GroupBy(s => s.Sport))
        {
            var pickIds = bySport.Select(s => s.PickId).ToList();
            var existing = await db.PickLiveNotificationStates
                .Where(s => s.Sport == bySport.Key && pickIds.Contains(s.PickId))
                .ToDictionaryAsync(s => s.PickId);

            foreach (var state in bySport)
            {
                if (existing.TryGetValue(state.PickId, out var row))
                {
                    row.LastNotifiedCovering = state.LastNotifiedCovering;
                    row.LastNotifiedAt = state.LastNotifiedAt;
                    row.FinalNotifiedAt = state.FinalNotifiedAt;
                    row.UpdatedAt = DateTimeOffset.UtcNow;
                }
                else
                {
                    state.UpdatedAt = DateTimeOffset.UtcNow;
                    db.PickLiveNotificationStates.Add(state);
                }
            }
        }

        await db.SaveChangesAsync();
    }
}
