using FourPlayWebApp.Server.Data;
using FourPlayWebApp.Server.Models.Data;
using FourPlayWebApp.Server.Services.Repositories.Interfaces;
using FourPlayWebApp.Shared.Models.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace FourPlayWebApp.Server.Services.Repositories;

// cache: the CFB control table (CfbSeasonWeekConfigs) is cached via ScheduleCache (saves evict
// it — ScheduleCacheInterceptor). Optional so tests that don't care can omit it (reads go
// straight to the database).
public class CfbRepository(IDbContextFactory<ApplicationDbContext> dbFactory, IMemoryCache? cache = null) : ICfbRepository {
    // frizat-b4g: a slate IS an in-scope control-table row, read straight from the one CFB table —
    // its label, dates and rules are the row's own, so an edited row reaches every reader.
    // Every read filters the cached whole table, which is already in (Season, week) order.
    public async Task<IEnumerable<CfbSeasonWeekConfig>> GetSlatesForSeasonAsync(int season) =>
        ScheduleCache.Copies((await WeekConfigRowsAsync()).Where(c => c.InScopeIvLeague && c.Season == season));

    public async Task<IEnumerable<CfbSeasonWeekConfig>> GetAllSlatesAsync() =>
        ScheduleCache.Copies((await WeekConfigRowsAsync()).Where(c => c.InScopeIvLeague));

    public async Task<CfbSeasonWeekConfig?> GetSlateByIdAsync(int slateId) =>
        ScheduleCache.Copy((await WeekConfigRowsAsync()).FirstOrDefault(c => c.InScopeIvLeague && c.Id == slateId));

    public async Task UpsertAsync(IEnumerable<CfbSpreads> spreads) {
        await using var db = await dbFactory.CreateDbContextAsync();
        var spreadList = spreads.ToList();
        var slateIds = spreadList.Select(s => s.CfbSlateId).ToHashSet();
        await UpsertByKeyAsync(
            db.CfbSpreads, spreadList,
            s => (s.CfbSlateId, s.HomeTeam),
            db.CfbSpreads.Where(s => slateIds.Contains(s.CfbSlateId)),
            (existing, spread) => {
                existing.AwayTeam         = spread.AwayTeam;
                // frizat: live incident 2026-09-19 — a manual re-fire of the CFB spread job
                // silently overwrote already-locked spread lines (including games with real picks
                // on them) with new ESPN odds, since this used to run unconditionally. The odds
                // themselves freeze once real (non-zero) values are captured — a locked line
                // shouldn't move after users have already picked against it. Matches
                // LeagueRepository.UpsertAsync's identical NFL guard (frizat-tf1) exactly.
                if (existing.HomeTeamSpread == 0 && existing.AwayTeamSpread == 0 &&
                    (spread.HomeTeamSpread != 0 || spread.AwayTeamSpread != 0)) {
                    existing.HomeTeamSpread = spread.HomeTeamSpread;
                    existing.AwayTeamSpread = spread.AwayTeamSpread;
                    existing.OverUnder      = spread.OverUnder;
                }
                // GameTime keeps refreshing regardless — a late schedule change can push a game's
                // real kickoff later than whatever was true when the spread first posted, and
                // GameHelpers.AllGamesStarted depends on this staying accurate.
                existing.GameTime         = spread.GameTime;
                existing.IsLeagueEligible = spread.IsLeagueEligible;
                // DateCreated intentionally NOT overwritten — preserves when the line was first posted.
            });
        await db.SaveChangesAsync();
    }

    public async Task<HashSet<(int Season, int Week)>> GetWeeksWithSpreadDataAsync() {
        await using var db = await dbFactory.CreateDbContextAsync();
        var pairs = await db.CfbSpreads
            .Join(db.CfbSeasonWeekConfigs.Where(c => c.IvLeagueWeekNumber != null), s => s.CfbSlateId, c => c.Id,
                (s, c) => new { c.Season, Week = c.IvLeagueWeekNumber!.Value })
            .Distinct()
            .ToListAsync();
        return pairs.Select(p => (p.Season, p.Week)).ToHashSet();
    }

    public async Task<IEnumerable<CfbSpreads>> GetSpreadsForSlateAsync(int cfbSlateId) {
        await using var db = await dbFactory.CreateDbContextAsync();
        return await db.CfbSpreads.Where(s => s.CfbSlateId == cfbSlateId).ToListAsync();
    }

    // Rank doesn't change once captured for a week, so a later capture (CfbRankingCaptureJob,
    // then CfbSpreadJob riding along with its odds fetch) overwrites the same row rather than
    // appending a new one — enforced by the unique index on (Season, WeekNumber, TeamAbbreviation).
    public async Task AddRankingsAsync(IEnumerable<CfbRanking> rankings) {
        await using var db = await dbFactory.CreateDbContextAsync();
        var rankingList = rankings.ToList();
        if (rankingList.Count == 0) return;

        // Every caller (CfbRankingCaptureJob's whole-season sweep, CfbSpreadJob's single-slate
        // capture) only ever produces rankings for one season per call — no need for a HashSet.
        var season = rankingList[0].Season;
        var weeks = rankingList.Select(r => r.WeekNumber).ToHashSet();
        await UpsertByKeyAsync(
            db.CfbRankings, rankingList,
            r => (r.Season, r.WeekNumber, r.TeamAbbreviation),
            db.CfbRankings.Where(r => r.Season == season && weeks.Contains(r.WeekNumber)),
            (existing, ranking) => {
                existing.CuratedRank   = ranking.CuratedRank;
                existing.EspnEventId   = ranking.EspnEventId;
                existing.CapturedAtUtc = ranking.CapturedAtUtc;
            });
        await db.SaveChangesAsync();
    }

    public async Task<Dictionary<string, int>> GetLatestRankingsForWeekAsync(int season, int weekNumber) {
        await using var db = await dbFactory.CreateDbContextAsync();
        return await db.CfbRankings
            .Where(r => r.Season == season && r.WeekNumber == weekNumber)
            .ToDictionaryAsync(r => r.TeamAbbreviation, r => r.CuratedRank);
    }

    public async Task<IEnumerable<CfbScores>> GetScoresForSlateAsync(int cfbSlateId) {
        await using var db = await dbFactory.CreateDbContextAsync();
        return await db.CfbScores.Where(s => s.CfbSlateId == cfbSlateId).ToListAsync();
    }

    // Subquery against the control table (translated server-side), not a local slate-id list
    // .Contains — see CLAUDE.md's Npgsql gotcha.
    public async Task<List<CfbSpreads>> GetLeagueEligibleSpreadsForSeasonAsync(int season) {
        await using var db = await dbFactory.CreateDbContextAsync();
        return await db.CfbSpreads.AsNoTracking()
            .Where(s => s.IsLeagueEligible && db.CfbSeasonWeekConfigs.Any(sl => sl.Id == s.CfbSlateId && sl.Season == season))
            .ToListAsync();
    }

    public async Task<List<CfbScores>> GetScoresForSeasonAsync(int season) {
        await using var db = await dbFactory.CreateDbContextAsync();
        return await db.CfbScores.AsNoTracking()
            .Where(s => db.CfbSeasonWeekConfigs.Any(sl => sl.Id == s.CfbSlateId && sl.Season == season))
            .ToListAsync();
    }

    private Task<IReadOnlyList<CfbSeasonWeekConfig>> WeekConfigRowsAsync() => ScheduleCache.CfbWeekConfigRowsAsync(cache, dbFactory);

    public async Task AddWeekConfigsAsync(IEnumerable<CfbSeasonWeekConfig> configs) {
        await using var db = await dbFactory.CreateDbContextAsync();
        db.CfbSeasonWeekConfigs.AddRange(configs);
        await db.SaveChangesAsync();
    }

    public async Task UpsertCfbScoresAsync(IEnumerable<CfbScores> scores) {
        await using var db = await dbFactory.CreateDbContextAsync();
        var scoreList = scores.ToList();
        var slateIds = scoreList.Select(s => s.CfbSlateId).ToHashSet();
        await UpsertByKeyAsync(
            db.CfbScores, scoreList,
            s => (s.CfbSlateId, s.HomeTeam),
            db.CfbScores.Where(s => slateIds.Contains(s.CfbSlateId)),
            (existing, score) => {
                existing.HomeTeamScore       = score.HomeTeamScore;
                existing.AwayTeamScore       = score.AwayTeamScore;
                existing.GameStatus          = score.GameStatus;
                existing.WeatherDisplayValue = score.WeatherDisplayValue;
                existing.WeatherConditionId  = score.WeatherConditionId;
                existing.WeatherTemperatureF = score.WeatherTemperatureF;
            });
        await db.SaveChangesAsync();
    }

    // Shared "insert new / update in place" upsert behind UpsertAsync, UpsertCfbScoresAsync, and
    // AddRankingsAsync — they differ only in entity type, composite key, the pre-filtered
    // existing-rows query (each caller narrows it to just the rows that could possibly collide),
    // and which fields carry over on update.
    private static async Task UpsertByKeyAsync<TEntity, TKey>(
        DbSet<TEntity> dbSet,
        List<TEntity> items,
        Func<TEntity, TKey> keySelector,
        IQueryable<TEntity> existingRows,
        Action<TEntity, TEntity> applyUpdate)
        where TEntity : class
        where TKey : notnull {
        var existingMap = await existingRows.ToDictionaryAsync(keySelector);
        foreach (var item in items) {
            if (!existingMap.TryGetValue(keySelector(item), out var existing))
                dbSet.Add(item);
            else
                applyUpdate(existing, item);
        }
    }
}
