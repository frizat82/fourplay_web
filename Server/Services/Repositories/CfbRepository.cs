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

    public async Task<CfbSlates?> GetSlateByIdAsync(int slateId) {
        await using var db = await dbFactory.CreateDbContextAsync();
        return await db.CfbSlates.FirstOrDefaultAsync(s => s.Id == slateId);
    }

    public async Task UpsertAsync(IEnumerable<CfbSpreads> spreads) {
        await using var db = await dbFactory.CreateDbContextAsync();
        var spreadList = spreads.ToList();
        var ids = spreadList.Select(s => s.EspnEventId).ToHashSet();
        var existingMap = await db.CfbSpreads
            .Where(s => ids.Contains(s.EspnEventId))
            .ToDictionaryAsync(s => s.EspnEventId);

        foreach (var spread in spreadList) {
            if (!existingMap.TryGetValue(spread.EspnEventId, out var existing))
                db.CfbSpreads.Add(spread);
            else {
                existing.CfbSlateId       = spread.CfbSlateId;
                existing.HomeTeam         = spread.HomeTeam;
                existing.AwayTeam         = spread.AwayTeam;
                existing.HomeTeamSpread   = spread.HomeTeamSpread;
                existing.AwayTeamSpread   = spread.AwayTeamSpread;
                existing.OverUnder        = spread.OverUnder;
                existing.GameTime         = spread.GameTime;
                existing.IsLeagueEligible = spread.IsLeagueEligible;
                // DateCreated intentionally NOT overwritten — preserves when the line was first posted.
            }
        }
        await db.SaveChangesAsync();
    }

    public async Task<HashSet<(int Season, int Week)>> GetWeeksWithSpreadDataAsync() {
        await using var db = await dbFactory.CreateDbContextAsync();
        var pairs = await db.CfbSpreads
            .Join(db.CfbSlates, s => s.CfbSlateId, sl => sl.Id, (s, sl) => new { sl.Season, sl.SlateNumber })
            .Distinct()
            .ToListAsync();
        return pairs.Select(p => (p.Season, p.SlateNumber)).ToHashSet();
    }

    public async Task<IEnumerable<CfbSpreads>> GetSpreadsForSlateAsync(int cfbSlateId) {
        await using var db = await dbFactory.CreateDbContextAsync();
        return await db.CfbSpreads.Where(s => s.CfbSlateId == cfbSlateId).ToListAsync();
    }

    public async Task AddRankingsAsync(IEnumerable<CfbRanking> rankings) {
        await using var db = await dbFactory.CreateDbContextAsync();
        db.CfbRankings.AddRange(rankings);
        await db.SaveChangesAsync();
    }

    public async Task<IEnumerable<CfbScores>> GetScoresForSlateAsync(int cfbSlateId) {
        await using var db = await dbFactory.CreateDbContextAsync();
        return await db.CfbScores.Where(s => s.CfbSlateId == cfbSlateId).ToListAsync();
    }

    public async Task<IEnumerable<CfbSeasonWeekConfig>> GetWeekConfigsForSeasonAsync(int season) {
        await using var db = await dbFactory.CreateDbContextAsync();
        return await db.CfbSeasonWeekConfigs
            .Where(c => c.Season == season)
            .OrderBy(c => c.EspnWeekNumber)
            .ToListAsync();
    }

    public async Task<IEnumerable<CfbSeasonWeekConfig>> GetAllWeekConfigsAsync() {
        await using var db = await dbFactory.CreateDbContextAsync();
        return await db.CfbSeasonWeekConfigs
            .OrderBy(c => c.Season).ThenBy(c => c.EspnWeekNumber)
            .ToListAsync();
    }

    public async Task AddWeekConfigsAsync(IEnumerable<CfbSeasonWeekConfig> configs) {
        await using var db = await dbFactory.CreateDbContextAsync();
        db.CfbSeasonWeekConfigs.AddRange(configs);
        await db.SaveChangesAsync();
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
