using FourPlayWebApp.Server.Data;
using FourPlayWebApp.Server.Services.Repositories.Interfaces;
using FourPlayWebApp.Shared.Models.Data;
using FourPlayWebApp.Shared.Models.Data.Dtos;
using Microsoft.EntityFrameworkCore;

namespace FourPlayWebApp.Server.Services.Repositories;

public class CfbPicksRepository(IDbContextFactory<ApplicationDbContext> dbFactory) : ICfbPicksRepository {
    public async Task<IEnumerable<CfbPicks>> GetUserPicksAsync(int leagueId, int cfbSlateId, string userId) {
        await using var db = await dbFactory.CreateDbContextAsync();
        return await db.CfbPicks
            .Where(p => p.LeagueId == leagueId && p.CfbSlateId == cfbSlateId && p.UserId == userId)
            .ToListAsync();
    }

    public async Task<IEnumerable<CfbPickDto>> GetAllPicksForSlateAsync(int leagueId, int cfbSlateId) {
        await using var db = await dbFactory.CreateDbContextAsync();
        return await db.CfbPicks
            .Where(p => p.LeagueId == leagueId && p.CfbSlateId == cfbSlateId)
            .Join(db.Users, p => p.UserId, u => u.Id, (p, u) => new CfbPickDto {
                Id         = p.Id,
                UserId     = p.UserId,
                UserName   = u.UserName ?? string.Empty,
                LeagueId   = p.LeagueId,
                CfbSlateId = p.CfbSlateId,
                EspnEventId = p.EspnEventId,
                Team       = p.Team,
                PickType   = p.PickType,
                Season     = p.Season,
            })
            .ToListAsync();
    }

    public async Task AddPicksAsync(IEnumerable<CfbPicks> picks) {
        await using var db = await dbFactory.CreateDbContextAsync();
        db.CfbPicks.AddRange(picks);
        await db.SaveChangesAsync();
    }

    public async Task DeletePicksAsync(int leagueId, int cfbSlateId, string userId) {
        await using var db = await dbFactory.CreateDbContextAsync();
        var picks = await db.CfbPicks
            .Where(p => p.LeagueId == leagueId && p.CfbSlateId == cfbSlateId && p.UserId == userId)
            .ToListAsync();
        db.CfbPicks.RemoveRange(picks);
        await db.SaveChangesAsync();
    }
}
