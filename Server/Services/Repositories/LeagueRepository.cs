using FourPlayWebApp.Server.Data;
using FourPlayWebApp.Server.Models.Data;
using FourPlayWebApp.Server.Models.Identity;
using FourPlayWebApp.Server.Services.Repositories.Interfaces;
using FourPlayWebApp.Shared.Models.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace FourPlayWebApp.Server.Services.Repositories;

public class LeagueRepository(IDbContextFactory<ApplicationDbContext> dbContextFactory, ILogger<LeagueRepository>? logger = null) : ILeagueRepository {
    private readonly ILogger<LeagueRepository> _logger = logger ?? NullLogger<LeagueRepository>.Instance;
    // League and User related methods
    public async Task<List<LeagueUserMapping>> GetLeagueUserMappingsAsync(int leagueId) {
        await using var db = await dbContextFactory.CreateDbContextAsync();
        return await db.LeagueUserMapping
            .Where(lum => lum.LeagueId == leagueId)
            .Include(lum => lum.User)
            .Include(lum => lum.League)
            .ToListAsync();
    }

    public async Task<List<LeagueUserMapping>> GetLeagueUserMappingsAsync(ApplicationUser user) {
        try {
            await using var db = await dbContextFactory.CreateDbContextAsync();
            return await db.LeagueUserMapping
                .Where(lum => lum.UserId == user.Id)
                .Include(lum => lum.User)
                .Include(lum => lum.League)
                .ToListAsync();
        }
        catch (Exception ex) {
            _logger.LogError(ex, "Failed to get league user mappings for user {UserId}", user?.Id);
            return new List<LeagueUserMapping>();
        }
    }

    public async Task<LeagueJuiceMapping?> GetLeagueJuiceMappingAsync(int leagueId, int season) {
        await using var db = await dbContextFactory.CreateDbContextAsync();
        try {
            return await db.LeagueJuiceMapping
                .Where(ljm => ljm.LeagueId == leagueId && ljm.Season == season)
                .Include(ljm => ljm.League)
                .FirstOrDefaultAsync();
        }
        catch (Exception ex) {
            _logger.LogError(ex, "Failed to get league juice mapping for league {LeagueId}, season {Season}", leagueId, season);
            return null;
        }
    }

    public async Task<List<LeagueJuiceMapping>> GetLeagueJuiceMappingAsync(int leagueId) {
        await using var db = await dbContextFactory.CreateDbContextAsync();
        try {
            return await db.LeagueJuiceMapping
                .Where(ljm => ljm.LeagueId == leagueId)
                .Include(ljm => ljm.League)
                .ToListAsync();
        }
        catch (Exception ex) {
            _logger.LogError(ex, "Failed to get league juice mappings for league {LeagueId}", leagueId);
            return new List<LeagueJuiceMapping>();
        }
    }

    public async Task<LeagueInfo> GetLeagueInfoAsync(int leagueId) {
        await using var db = await dbContextFactory.CreateDbContextAsync();
        return await db.LeagueInfo.Where(x => x.Id == leagueId)
            .Include(li => li.LeagueJuiceMappings)
            .Include(li => li.LeagueUsers)
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
    public async Task AddNewNflSpreadsAsync(List<NflSpreads> spreads) {
        await using var db = await dbContextFactory.CreateDbContextAsync();

        foreach (var spread in spreads) {
            var existing = await db.NflSpreads.FirstOrDefaultAsync(s =>
                s.Season == spread.Season &&
                s.NflWeek == spread.NflWeek &&
                s.HomeTeam == spread.HomeTeam);

            if (existing == null) {
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


    // Add operations
    public async Task AddLeagueUserAsync(LeagueUsers leagueUser) {
        await using var db = await dbContextFactory.CreateDbContextAsync();
        await db.LeagueUsers.AddAsync(leagueUser);
        await db.SaveChangesAsync();
    }

    public async Task AddLeagueUserMappingAsync(LeagueUserMapping mapping) {
        await using var db = await dbContextFactory.CreateDbContextAsync();
        await db.LeagueUserMapping.AddAsync(mapping);
        await db.SaveChangesAsync();
    }

    public async Task AddLeagueInfoAsync(LeagueInfo leagueInfo) {
        await using var db = await dbContextFactory.CreateDbContextAsync();
        await db.LeagueInfo.AddAsync(leagueInfo);
        await db.SaveChangesAsync();
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
            .AnyAsync(lum => lum.UserId == userId && lum.LeagueId == leagueId);
    }
}
