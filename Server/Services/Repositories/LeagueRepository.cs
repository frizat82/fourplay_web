using FourPlayWebApp.Server.Data;
using FourPlayWebApp.Server.Models.Data;
using FourPlayWebApp.Server.Models.Identity;
using FourPlayWebApp.Server.Services.Repositories.Interfaces;
using FourPlayWebApp.Shared.Models.Data;
using FourPlayWebApp.Shared.Models.Enum;
using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;

namespace FourPlayWebApp.Server.Services.Repositories;

public class LeagueRepository(IDbContextFactory<ApplicationDbContext> dbContextFactory) : ILeagueRepository {
    // League and User related methods
    public async Task<List<LeagueUserMapping>> GetLeagueUserMappingsAsync(int leagueId) {
        await using var db = await dbContextFactory.CreateDbContextAsync();
        return await db.LeagueUserMapping
            .Where(lum => lum.LeagueId == leagueId && lum.IsActive)
            .Include(lum => lum.User)
            .Include(lum => lum.League)
            .ToListAsync();
    }

    public async Task<List<LeagueUserMapping>> GetLeagueUserMappingsAsync(ApplicationUser user) {
        await using var db = await dbContextFactory.CreateDbContextAsync();
        return await db.LeagueUserMapping
            .Where(lum => lum.UserId == user.Id && lum.IsActive)
            .Include(lum => lum.User)
            .Include(lum => lum.League)
            .ToListAsync();
    }

    public async Task<LeagueJuiceMapping?> GetLeagueJuiceMappingAsync(int leagueId, int season) {
        await using var db = await dbContextFactory.CreateDbContextAsync();
        return await db.LeagueJuiceMapping
            .Where(ljm => ljm.LeagueId == leagueId && ljm.Season == season)
            .Include(ljm => ljm.League)
            .FirstOrDefaultAsync();
    }

    public async Task<List<LeagueJuiceMapping>> GetLeagueJuiceMappingAsync(int leagueId) {
        await using var db = await dbContextFactory.CreateDbContextAsync();
        return await db.LeagueJuiceMapping
            .Where(ljm => ljm.LeagueId == leagueId)
            .Include(ljm => ljm.League)
            .ToListAsync();
    }

    public async Task<LeagueInfo> GetLeagueInfoAsync(int leagueId) {
        await using var db = await dbContextFactory.CreateDbContextAsync();
        return await db.LeagueInfo.Where(x => x.Id == leagueId)
            .Include(li => li.LeagueJuiceMappings)
            .Include(li => li.LeagueUserMappings)
            .FirstAsync();
    }

    public async Task<List<ApplicationUser>> GetUsersAsync() {
        await using var db = await dbContextFactory.CreateDbContextAsync();
        return await db.Users.ToListAsync();
    }

    public async Task<LeagueInfo?> GetLeagueByNameAsync(string leagueName) {
        await using var db = await dbContextFactory.CreateDbContextAsync();
        return await db.LeagueInfo
            .Include(li => li.LeagueJuiceMappings)
            .FirstOrDefaultAsync(li => li.LeagueName == leagueName);
    }

    // NFL Season Week Config
    public async Task<List<NflSeasonWeekConfig>> GetNflSeasonWeekConfigsAsync() {
        await using var db = await dbContextFactory.CreateDbContextAsync();
        return await db.NflSeasonWeekConfigs.OrderBy(c => c.Season).ThenBy(c => c.WeekId).ToListAsync();
    }

    // NFL Weeks
    public async Task UpsertNflWeeksAsync(List<NflWeeks> weeks)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync();

        // ✅ Check for duplicates in the input list
        var duplicateGroups = weeks
            .GroupBy(w => new { w.Season, w.NflWeek })
            .Where(g => g.Count() > 1)
            .ToList();

        if (duplicateGroups.Any())
        {
            var dupes = string.Join(", ",
                duplicateGroups.Select(g => $"Season {g.Key.Season}, Week {g.Key.NflWeek}"));
            throw new InvalidOperationException($"Duplicate Season/Week combinations in input: {dupes}");
        }

        foreach (var week in weeks)
        {
            var existing = await db.NflWeeks
                .FirstOrDefaultAsync(s =>
                    s.Season == week.Season &&
                    s.NflWeek == week.NflWeek);

            if (existing != null)
            {
                week.Id = existing.Id;
                db.Entry(existing).CurrentValues.SetValues(week);
            }
            else
            {
                await db.NflWeeks.AddAsync(week);
            }
        }

        await db.SaveChangesAsync();
    }


    public async Task<List<NflWeeks>> GetNflWeeksAsync(int season) {
        await using var db = await dbContextFactory.CreateDbContextAsync();
        return await db.NflWeeks
            .Where(score => score.Season == season)
            .ToListAsync();
    }


    // NFL Scores and Spreads methods
    public async Task UpsertAsync(IEnumerable<NflSpreads> spreads) {
        await using var db = await dbContextFactory.CreateDbContextAsync();
        var spreadList = spreads.ToList();

        // Batch the existing-row lookup into one query (mirrors CfbRepository.UpsertAsync) instead
        // of a per-spread FirstOrDefaultAsync — avoids an N+1 round-trip on every spread fetch.
        var seasons = spreadList.Select(s => s.Season).ToHashSet();
        var weeks = spreadList.Select(s => s.NflWeek).ToHashSet();
        var existingMap = await db.NflSpreads
            .Where(s => seasons.Contains(s.Season) && weeks.Contains(s.NflWeek))
            .ToDictionaryAsync(s => (s.Season, s.NflWeek, s.HomeTeam));

        foreach (var spread in spreadList) {
            if (!existingMap.TryGetValue((spread.Season, spread.NflWeek, spread.HomeTeam), out var existing)) {
                // Doesn't exist -> Insert new record
                await db.NflSpreads.AddAsync(spread);
            }
            else {
                // Exists -> Update only if odds are currently 0/0 and new ones are valid
                if (existing.HomeTeamSpread == 0 && existing.AwayTeamSpread == 0 &&
                    (spread.HomeTeamSpread != 0 || spread.AwayTeamSpread != 0)) {
                    // Update fields
                    spread.Id = existing.Id;
                    db.Entry(existing).CurrentValues.SetValues(spread);
                }
            }
        }

        await db.SaveChangesAsync();
    }

    public async Task<HashSet<(int Season, int Week)>> GetWeeksWithSpreadDataAsync() {
        await using var db = await dbContextFactory.CreateDbContextAsync();
        var pairs = await db.NflSpreads
            .Select(s => new { s.Season, s.NflWeek })
            .Distinct()
            .ToListAsync();
        return pairs.Select(p => (p.Season, p.NflWeek)).ToHashSet();
    }


    public async Task UpsertNflScoresAsync(List<NflScores> scores) {
        await using var db = await dbContextFactory.CreateDbContextAsync();

        foreach (var score in scores) {
            var existing = await db.NflScores
                .FirstOrDefaultAsync(s =>
                    s.Season == score.Season &&
                    s.NflWeek == score.NflWeek &&
                    s.HomeTeam == score.HomeTeam);

            if (existing != null) {
                // Update fields
                score.Id = existing.Id;
                db.Entry(existing).CurrentValues.SetValues(score);
            }
            else {
                await db.NflScores.AddAsync(score);
            }
        }

        await db.SaveChangesAsync();
    }

    public async Task<List<NflScores>> GetNflScoresAsync(int season, int week) {
        await using var db = await dbContextFactory.CreateDbContextAsync();
        return await db.NflScores
            .Where(score => score.Season == season && score.NflWeek == week)
            .ToListAsync();
    }

    public async Task<List<NflScores>> GetAllNflScoresForSeasonAsync(int season) {
        await using var db = await dbContextFactory.CreateDbContextAsync();
        return await db.NflScores
            .Where(score => score.Season == season)
            .OrderBy(score => score.NflWeek)
            .ToListAsync();
    }

    public async Task<List<NflSpreads>?> GetNflSpreadsAsync(int season, int week) {
        await using var db = await dbContextFactory.CreateDbContextAsync();
        return await db.NflSpreads
            .Where(spread => spread.Season == season && spread.NflWeek == week)
            .ToListAsync();
    }

    public async Task<List<NflSpreads>> GetAllNflSpreadsForSeasonAsync(int season) {
        await using var db = await dbContextFactory.CreateDbContextAsync();
        return await db.NflSpreads
            .Where(spread => spread.Season == season)
            .OrderBy(spread => spread.NflWeek)
            .ToListAsync();
    }

    // NFL Picks methods
    public async Task<List<NflPicks>> GetNflPicksAsync(int leagueId, int season, int week) {
        await using var db = await dbContextFactory.CreateDbContextAsync();
        return await db.NflPicks
            .Where(pick => pick.LeagueId == leagueId && pick.Season == season && pick.NflWeek == week)
            .Include(pick => pick.User)
            .ToListAsync();
    }

    public async Task<List<NflPicks>> GetUserNflPicksAsync(string userId, int leagueId, int season, int week) {
        await using var db = await dbContextFactory.CreateDbContextAsync();
        return await db.NflPicks
            .Where(pick => pick.UserId == userId && pick.LeagueId == leagueId &&
                           pick.Season == season && pick.NflWeek == week)
            .Include(pick => pick.User)
            .ToListAsync();
    }


    // Commissioner portal methods
    public async Task<List<LeagueInfo>> GetLeaguesByOwnerAsync(string ownerId) {
        await using var db = await dbContextFactory.CreateDbContextAsync();
        return await db.LeagueInfo
            .Where(l => l.OwnerUserId == ownerId)
            .Include(l => l.LeagueJuiceMappings)
            .ToListAsync();
    }

    public async Task<List<LeagueInfo>> GetAllLeaguesAsync() {
        await using var db = await dbContextFactory.CreateDbContextAsync();
        return await db.LeagueInfo
            .Include(l => l.LeagueJuiceMappings)
            .ToListAsync();
    }

    public async Task UpdateLeagueOwnerAsync(int leagueId, string newOwnerUserId) {
        await using var db = await dbContextFactory.CreateDbContextAsync();
        var league = await db.LeagueInfo.FirstAsync(l => l.Id == leagueId);
        league.OwnerUserId = newOwnerUserId;
        await db.SaveChangesAsync();
    }

    public async Task UpdateLeagueJuiceMappingAsync(LeagueJuiceMapping mapping) {
        await using var db = await dbContextFactory.CreateDbContextAsync();
        db.LeagueJuiceMapping.Update(mapping);
        await db.SaveChangesAsync();
    }

    // Soft-delete — keeps the row (and the member's pick history) for audit purposes, just
    // excluded from active-membership reads (see the IsActive filters throughout this class).
    public async Task RemoveLeagueUserMappingAsync(int leagueId, string userId) {
        await using var db = await dbContextFactory.CreateDbContextAsync();
        var mapping = await db.LeagueUserMapping
            .FirstOrDefaultAsync(m => m.LeagueId == leagueId && m.UserId == userId);
        if (mapping is not null) {
            mapping.IsActive = false;
            mapping.RemovedAt = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync();
        }
    }

    // Shared with DemoDataSeeder.PurgeUnknownLeaguesAsync via LeagueCascadeDelete — one place for
    // the ordered multi-table cascade instead of two independently-drifting copies.
    public async Task DeleteLeagueAsync(int leagueId) {
        await using var db = await dbContextFactory.CreateDbContextAsync();
        LeagueCascadeDelete.RemoveLeaguesAndDependents(db, [leagueId]);
        await db.SaveChangesAsync();
    }

    public async Task<int> GetLeagueMemberCountAsync(int leagueId) {
        await using var db = await dbContextFactory.CreateDbContextAsync();
        return await db.LeagueUserMapping.CountAsync(m => m.LeagueId == leagueId && m.IsActive);
    }

    public async Task<int> GetLeagueMemberCountAsync(int leagueId, int season, LeagueType leagueType) {
        await using var db = await dbContextFactory.CreateDbContextAsync();
        var range = await GetSeasonDateRangeAsync(db, season, leagueType);
        if (range is null) return 0;
        var (start, end) = range.Value;
        return await db.LeagueUserMapping
            .Where(m => m.LeagueId == leagueId)
            .Where(ActiveDuringWindow(start, end))
            .CountAsync();
    }

    public async Task<Dictionary<int, int>> GetLeagueMemberCountsAsync(int season) {
        await using var db = await dbContextFactory.CreateDbContextAsync();
        var result = new Dictionary<int, int>();
        foreach (var sport in new[] { LeagueType.Nfl, LeagueType.Cfb }) {
            var range = await GetSeasonDateRangeAsync(db, season, sport);
            if (range is null) continue;
            var (start, end) = range.Value;
            var counts = await db.LeagueUserMapping
                .Where(m => m.League.LeagueType == sport)
                .Where(ActiveDuringWindow(start, end))
                .GroupBy(m => m.LeagueId)
                .Select(g => new { LeagueId = g.Key, Count = g.Count() })
                .ToListAsync();
            foreach (var c in counts) result[c.LeagueId] = c.Count;
        }
        return result;
    }

    private static Expression<Func<LeagueUserMapping, bool>> ActiveDuringWindow(DateTimeOffset start, DateTimeOffset end) =>
        m => m.DateCreated <= end && (m.RemovedAt == null || m.RemovedAt >= start);

    // Computed client-side (not ORDER BY in SQL) — avoids the SQLite/EF DateTimeOffset ORDER BY
    // translation gap that bit LeagueMembershipInviteService; MIN/MAX over an already-materialized
    // list sidesteps it entirely and this table is small (one row per week per season).
    private static async Task<(DateTimeOffset Start, DateTimeOffset End)?> GetSeasonDateRangeAsync(
        ApplicationDbContext db, int season, LeagueType leagueType) {
        if (leagueType == LeagueType.Cfb) {
            var weeks = await db.CfbSeasonWeekConfigs.Where(c => c.Season == season).ToListAsync();
            if (weeks.Count == 0) return null;
            var start = weeks.Min(w => w.WeekStartDate).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
            var end = weeks.Max(w => w.WeekEndDate).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
            return (start, end);
        } else {
            var weeks = await db.NflSeasonWeekConfigs.Where(c => c.Season == season).ToListAsync();
            if (weeks.Count == 0) return null;
            return (weeks.Min(w => w.WeekStartDatetime), weeks.Max(w => w.WeekEndDatetime));
        }
    }

    // Add operations
    // (LeagueId, UserId) is unique — a removed-then-re-added user must reactivate their existing
    // (soft-deleted) row rather than insert a second one, or this throws a unique-index violation
    // against real Postgres. Preserves the original DateCreated (join date), not a re-join date.
    public async Task AddLeagueUserMappingAsync(LeagueUserMapping mapping) {
        await using var db = await dbContextFactory.CreateDbContextAsync();
        var existing = await db.LeagueUserMapping
            .FirstOrDefaultAsync(m => m.LeagueId == mapping.LeagueId && m.UserId == mapping.UserId);
        if (existing is not null) {
            existing.IsActive = true;
            existing.RemovedAt = null;
        } else {
            await db.LeagueUserMapping.AddAsync(mapping);
        }
        await db.SaveChangesAsync();
    }

    public async Task<LeagueInfo> AddLeagueInfoAsync(LeagueInfo leagueInfo) {
        await using var db = await dbContextFactory.CreateDbContextAsync();
        await db.LeagueInfo.AddAsync(leagueInfo);
        await db.SaveChangesAsync();
        return leagueInfo;
    }

    public async Task AddLeagueJuiceMappingAsync(LeagueJuiceMapping mapping) {
        await using var db = await dbContextFactory.CreateDbContextAsync();
        await db.LeagueJuiceMapping.AddAsync(mapping);
        await db.SaveChangesAsync();
    }

    public async Task AddNflScoresAsync(IEnumerable<NflScores> scores) {
        await using var db = await dbContextFactory.CreateDbContextAsync();
        await db.NflScores.AddRangeAsync(scores);
        await db.SaveChangesAsync();
    }

    public async Task AddNflSpreadsAsync(IEnumerable<NflSpreads> spreads) {
        await using var db = await dbContextFactory.CreateDbContextAsync();
        await db.NflSpreads.AddRangeAsync(spreads);
        await db.SaveChangesAsync();
    }

    public async Task AddNflPicksAsync(IEnumerable<NflPicks> picks) {
        await using var db = await dbContextFactory.CreateDbContextAsync();
        await db.NflPicks.AddRangeAsync(picks);
        await db.SaveChangesAsync();
    }


// Remove operations
    public async Task RemoveNflScoresAsync(IEnumerable<NflScores> scores) {
        await using var db = await dbContextFactory.CreateDbContextAsync();
        db.NflScores.RemoveRange(scores);
        await db.SaveChangesAsync();
    }

    public async Task RemoveNflSpreadsAsync(IEnumerable<NflSpreads> spreads) {
        await using var db = await dbContextFactory.CreateDbContextAsync();
        db.NflSpreads.RemoveRange(spreads);
        await db.SaveChangesAsync();
    }

    public async Task RemoveNflPicksAsync(IEnumerable<NflPicks> picks) {
        await using var db = await dbContextFactory.CreateDbContextAsync();
        db.NflPicks.RemoveRange(picks);
        await db.SaveChangesAsync();
    }

    // Utility methods

    public async Task<bool> LeagueExistsAsync(string leagueName, int season) {
        await using var db = await dbContextFactory.CreateDbContextAsync();
        return await db.LeagueJuiceMapping
            .AnyAsync(ljm => ljm.League.LeagueName == leagueName && ljm.Season == season);
    }
    public async Task<bool> LeagueExistsAsync(string leagueName) {
        await using var db = await dbContextFactory.CreateDbContextAsync();
        return await db.LeagueInfo
            .AnyAsync(ljm => ljm.LeagueName == leagueName);
    }

    public async Task<bool> UserExistsInLeagueAsync(string userId, int leagueId) {
        await using var db = await dbContextFactory.CreateDbContextAsync();
        return await db.LeagueUserMapping
            .AnyAsync(lum => lum.UserId == userId && lum.LeagueId == leagueId && lum.IsActive);
    }
}
