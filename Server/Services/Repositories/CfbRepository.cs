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

    public async Task<bool> DeleteSlatesAsync(IEnumerable<CfbSlates> slates) {
        await using var db = await dbFactory.CreateDbContextAsync();
        var slateList = slates.ToList();
        var ids = slateList.Select(s => s.Id).ToHashSet();

        // frizat-2lc: never bulk-delete slates that already carry real dependent data — guard
        // lives here, not just in the one caller that exists today, so any future caller of
        // DeleteSlatesAsync gets the same protection against real data loss / an unhandled FK
        // violation (CfbSpreads/CfbScores/CfbPicks.CfbSlateId is Restrict, not Cascade).
        var hasDependentData = ids.Count > 0 &&
            (await db.CfbSpreads.AnyAsync(s => ids.Contains(s.CfbSlateId))
             || await db.CfbScores.AnyAsync(s => ids.Contains(s.CfbSlateId))
             || await db.CfbPicks.AnyAsync(p => ids.Contains(p.CfbSlateId)));
        if (hasDependentData) return false;

        db.CfbSlates.RemoveRange(slateList);
        await db.SaveChangesAsync();
        return true;
    }

    public async Task<IEnumerable<CfbSlates>> GetSlatesForSeasonAsync(int season) {
        await using var db = await dbFactory.CreateDbContextAsync();
        return await db.CfbSlates
            .Where(s => s.Season == season)
            .OrderBy(s => s.SlateNumber)
            .ToListAsync();
    }

    public async Task<IEnumerable<CfbSlates>> GetAllSlatesAsync() {
        await using var db = await dbFactory.CreateDbContextAsync();
        return await db.CfbSlates
            .OrderBy(s => s.Season).ThenBy(s => s.SlateNumber)
            .ToListAsync();
    }

    public async Task<CfbSlates?> GetSlateByIdAsync(int slateId) {
        await using var db = await dbFactory.CreateDbContextAsync();
        return await db.CfbSlates.FirstOrDefaultAsync(s => s.Id == slateId);
    }

    public async Task UpsertAsync(IEnumerable<CfbSpreads> spreads) {
        await using var db = await dbFactory.CreateDbContextAsync();
        var spreadList = spreads.ToList();
        var slateIds = spreadList.Select(s => s.CfbSlateId).ToHashSet();
        var existingMap = await db.CfbSpreads
            .Where(s => slateIds.Contains(s.CfbSlateId))
            .ToDictionaryAsync(s => (s.CfbSlateId, s.HomeTeam));

        foreach (var spread in spreadList) {
            if (!existingMap.TryGetValue((spread.CfbSlateId, spread.HomeTeam), out var existing))
                db.CfbSpreads.Add(spread);
            else {
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

    // Rank doesn't change once captured for a week, so a later capture (CfbRankingCaptureJob,
    // then CfbSpreadJob riding along with its odds fetch) overwrites the same row rather than
    // appending a new one — enforced by the unique index on (Season, EspnWeekNumber, TeamAbbreviation).
    public async Task AddRankingsAsync(IEnumerable<CfbRanking> rankings) {
        await using var db = await dbFactory.CreateDbContextAsync();
        var rankingList = rankings.ToList();
        if (rankingList.Count == 0) return;

        var seasons = rankingList.Select(r => r.Season).ToHashSet();
        var weeks = rankingList.Select(r => r.EspnWeekNumber).ToHashSet();
        var existingMap = await db.CfbRankings
            .Where(r => seasons.Contains(r.Season) && weeks.Contains(r.EspnWeekNumber))
            .ToDictionaryAsync(r => (r.Season, r.EspnWeekNumber, r.TeamAbbreviation));

        foreach (var ranking in rankingList) {
            if (!existingMap.TryGetValue((ranking.Season, ranking.EspnWeekNumber, ranking.TeamAbbreviation), out var existing))
                db.CfbRankings.Add(ranking);
            else {
                existing.CuratedRank   = ranking.CuratedRank;
                existing.EspnEventId   = ranking.EspnEventId;
                existing.CapturedAtUtc = ranking.CapturedAtUtc;
            }
        }
        await db.SaveChangesAsync();
    }

    public async Task<Dictionary<string, int>> GetLatestRankingsForWeekAsync(int season, int espnWeekNumber) {
        await using var db = await dbFactory.CreateDbContextAsync();
        return await db.CfbRankings
            .Where(r => r.Season == season && r.EspnWeekNumber == espnWeekNumber)
            .ToDictionaryAsync(r => r.TeamAbbreviation, r => r.CuratedRank);
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
        var slateIds = scoreList.Select(s => s.CfbSlateId).ToHashSet();
        var existingMap = await db.CfbScores
            .Where(s => slateIds.Contains(s.CfbSlateId))
            .ToDictionaryAsync(s => (s.CfbSlateId, s.HomeTeam));

        foreach (var score in scoreList) {
            if (!existingMap.TryGetValue((score.CfbSlateId, score.HomeTeam), out var existing))
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
