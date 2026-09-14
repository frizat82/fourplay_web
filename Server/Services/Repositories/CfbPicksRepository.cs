using FourPlayWebApp.Server.Data;
using FourPlayWebApp.Server.Services.Repositories.Interfaces;
using FourPlayWebApp.Shared.Models.Data;
using FourPlayWebApp.Shared.Models.Data.Dtos;
using FourPlayWebApp.Shared.Models.Enum;
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

    public async Task<bool> TryAddPicksAsync(IEnumerable<CfbPicks> newPicks, string userId, int leagueId, int season, int cfbSlateId, int requiredPicks) {
        var picksList = newPicks.ToList();
        if (picksList.Count == 0) return true;

        await using var db = await dbFactory.CreateDbContextAsync();
        return await PickConcurrencyGuard.RunLockedAsync(db, $"cfb:{userId}:{leagueId}:{season}:{cfbSlateId}", async () => {
            var existingCount = await db.CfbPicks.CountAsync(p =>
                p.UserId == userId && p.LeagueId == leagueId && p.Season == season && p.CfbSlateId == cfbSlateId);
            if (existingCount + picksList.Count > requiredPicks) return false;

            db.CfbPicks.AddRange(picksList);
            await db.SaveChangesAsync();
            return true;
        });
    }

    public async Task<bool> TryRemovePickAsync(string userId, int leagueId, int season, int cfbSlateId, string team, PickType pickType) {
        await using var db = await dbFactory.CreateDbContextAsync();
        return await PickConcurrencyGuard.RunLockedAsync(db, $"cfb:{userId}:{leagueId}:{season}:{cfbSlateId}", async () => {
            var pick = await db.CfbPicks.FirstOrDefaultAsync(p =>
                p.UserId == userId && p.LeagueId == leagueId && p.Season == season && p.CfbSlateId == cfbSlateId &&
                p.Team == team && p.PickType == pickType);
            if (pick is null) return false;

            db.CfbPicks.Remove(pick);
            await db.SaveChangesAsync();
            return true;
        });
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
