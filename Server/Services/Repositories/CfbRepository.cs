using FourPlayWebApp.Server.Data;
using FourPlayWebApp.Server.Models.Data;
using FourPlayWebApp.Server.Services.Repositories.Interfaces;
using FourPlayWebApp.Shared.Models.Data;
using Microsoft.EntityFrameworkCore;

namespace FourPlayWebApp.Server.Services.Repositories;

public class CfbRepository(IDbContextFactory<ApplicationDbContext> dbFactory) : ICfbRepository {
    public async Task<bool> SlatesExistForSeasonAsync(int season) {
        await using var db = await dbFactory.CreateDbContextAsync();
        return await db.CfbSlates.AnyAsync(s => s.Season == season);
    }

    public async Task AddSlatesAsync(IEnumerable<CfbSlates> slates) {
        await using var db = await dbFactory.CreateDbContextAsync();
        db.CfbSlates.AddRange(slates);
        await db.SaveChangesAsync();
    }

    public async Task DeleteSlatesAsync(IEnumerable<CfbSlates> slates) {
        await using var db = await dbFactory.CreateDbContextAsync();
        db.CfbSlates.RemoveRange(slates);
        await db.SaveChangesAsync();
    }

    public async Task<IEnumerable<CfbSlates>> GetSlatesForSeasonAsync(int season) {
        await using var db = await dbFactory.CreateDbContextAsync();
        return await db.CfbSlates
            .Where(s => s.Season == season)
            .OrderBy(s => s.SlateNumber)
            .ToListAsync();
    }

    public async Task AddCfbSpreadsAsync(IEnumerable<CfbSpreads> spreads) {
        await using var db = await dbFactory.CreateDbContextAsync();
        db.CfbSpreads.AddRange(spreads);
        await db.SaveChangesAsync();
    }

    public async Task<IEnumerable<CfbSpreads>> GetSpreadsForSlateAsync(int cfbSlateId) {
        await using var db = await dbFactory.CreateDbContextAsync();
        return await db.CfbSpreads.Where(s => s.CfbSlateId == cfbSlateId).ToListAsync();
    }

    public async Task<IEnumerable<CfbScores>> GetScoresForSlateAsync(int cfbSlateId) {
        await using var db = await dbFactory.CreateDbContextAsync();
        return await db.CfbScores.Where(s => s.CfbSlateId == cfbSlateId).ToListAsync();
    }

    public async Task UpsertCfbScoresAsync(IEnumerable<CfbScores> scores) {
        await using var db = await dbFactory.CreateDbContextAsync();
        var scoreList = scores.ToList();
        var ids = scoreList.Select(s => s.EspnEventId).ToHashSet();
        var existingMap = await db.CfbScores
            .Where(s => ids.Contains(s.EspnEventId))
            .ToDictionaryAsync(s => s.EspnEventId);

        foreach (var score in scoreList) {
            if (!existingMap.TryGetValue(score.EspnEventId, out var existing))
                db.CfbScores.Add(score);
            else {
                existing.HomeTeamScore       = score.HomeTeamScore;
                existing.AwayTeamScore       = score.AwayTeamScore;
                existing.GameStatus          = score.GameStatus;
                existing.WeatherDisplayValue = score.WeatherDisplayValue;
                existing.WeatherConditionId  = score.WeatherConditionId;
                existing.WeatherTemperatureF = score.WeatherTemperatureF;
            }
        }
        await db.SaveChangesAsync();
    }
}
