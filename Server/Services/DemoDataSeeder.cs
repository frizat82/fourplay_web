using FourPlayWebApp.Server.Data;
using FourPlayWebApp.Server.Models.Data;
using FourPlayWebApp.Server.Models.Identity;
using FourPlayWebApp.Shared.Models.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Serilog;

namespace FourPlayWebApp.Server.Services;

/// <summary>
/// Seeds demo data so the full UI is explorable locally without a live NFL season.
/// Only runs when DEMO_MODE=true. Idempotent — safe to call on every startup.
/// </summary>
public class DemoDataSeeder(ApplicationDbContext db, UserManager<ApplicationUser> userManager, IConfiguration configuration)
{
    private const int DemoSeason = 2023;
    private const int DemoWeek = 8;

    public async Task SeedAsync()
    {
        Log.Information("DemoDataSeeder: starting seed for season {Season} week {Week}", DemoSeason, DemoWeek);

        await SeedNflWeekAsync();
        await SeedSpreadsAsync();
        var league = await SeedLeagueAsync();
        await SeedLeagueMemberAsync(league);

        Log.Information("DemoDataSeeder: seed complete");
    }

    private async Task SeedNflWeekAsync()
    {
        if (await db.NflWeeks.AnyAsync(w => w.Season == DemoSeason && w.NflWeek == DemoWeek))
            return;

        db.NflWeeks.Add(new NflWeeks
        {
            Season = DemoSeason,
            NflWeek = DemoWeek,
            StartDate = new DateTimeOffset(2023, 10, 26, 0, 0, 0, TimeSpan.Zero),
            EndDate = new DateTimeOffset(2023, 10, 31, 23, 59, 59, TimeSpan.Zero),
        });
        await db.SaveChangesAsync();
        Log.Information("DemoDataSeeder: seeded NflWeek {Season}/{Week}", DemoSeason, DemoWeek);
    }

    private async Task SeedSpreadsAsync()
    {
        if (await db.NflSpreads.AnyAsync(s => s.Season == DemoSeason && s.NflWeek == DemoWeek))
        {
            var count = await db.NflSpreads.CountAsync(s => s.Season == DemoSeason && s.NflWeek == DemoWeek);
            Log.Information("DemoDataSeeder: spreads already seeded ({Count} rows)", count);
            return;
        }

        var spreads = new List<NflSpreads>
        {
            Spread("BUF", "TB",  -3.0,  3.0, 48.5, "2023-10-27T00:15:00Z"),
            Spread("DAL", "LAR", -6.5,  6.5, 46.5, "2023-10-29T17:00:00Z"),
            Spread("GB",  "MIN", -3.0,  3.0, 44.5, "2023-10-29T17:00:00Z"),
            Spread("TEN", "ATL",  1.5, -1.5, 38.5, "2023-10-29T17:00:00Z"),
            Spread("IND", "NO",  -2.5,  2.5, 41.5, "2023-10-29T17:00:00Z"),
            Spread("MIA", "NE",  -7.0,  7.0, 43.5, "2023-10-29T17:00:00Z"),
            Spread("NYG", "NYJ",  3.0, -3.0, 36.5, "2023-10-29T17:00:00Z"),
            Spread("PIT", "JAX", -1.0,  1.0, 40.5, "2023-10-29T17:00:00Z"),
            Spread("WSH", "PHI",  9.5, -9.5, 44.5, "2023-10-29T17:00:00Z"),
            Spread("CAR", "HOU",  3.5, -3.5, 39.5, "2023-10-29T17:00:00Z"),
            Spread("SEA", "CLE", -3.0,  3.0, 43.5, "2023-10-29T20:05:00Z"),
            Spread("DEN", "KC",  10.5,-10.5, 51.5, "2023-10-29T20:25:00Z"),
            Spread("ARI", "BAL", 13.5,-13.5, 44.5, "2023-10-29T20:25:00Z"),
            Spread("SF",  "CIN", -3.5,  3.5, 46.5, "2023-10-29T20:25:00Z"),
            Spread("LAC", "CHI", -7.0,  7.0, 41.5, "2023-10-30T00:20:00Z"),
            Spread("DET", "LV",  -7.5,  7.5, 47.5, "2023-10-31T00:15:00Z"),
        };

        db.NflSpreads.AddRange(spreads);
        await db.SaveChangesAsync();
        Log.Information("DemoDataSeeder: seeded {Count} spreads", spreads.Count);
    }

    private async Task<LeagueInfo?> SeedLeagueAsync()
    {
        var existing = await db.LeagueInfo.FirstOrDefaultAsync(l => l.LeagueName == "Demo League");
        if (existing != null)
            return existing;

        var adminEmail = configuration["ADMIN_EMAIL"] ?? throw new InvalidOperationException("ADMIN_EMAIL required");
        var adminUser = await userManager.FindByEmailAsync(adminEmail);
        if (adminUser == null)
        {
            Log.Warning("DemoDataSeeder: admin user not created yet — league seeding deferred (UserManagerJob will complete it)");
            return null;
        }

        var league = new LeagueInfo
        {
            LeagueName = "Demo League",
            OwnerUserId = adminUser.Id,
        };
        db.LeagueInfo.Add(league);
        await db.SaveChangesAsync();
        Log.Information("DemoDataSeeder: created Demo League (id={Id})", league.Id);
        return league;
    }

    private async Task SeedLeagueMemberAsync(LeagueInfo? league)
    {
        if (league == null) return;

        var adminEmail = configuration["ADMIN_EMAIL"] ?? throw new InvalidOperationException("ADMIN_EMAIL required");
        var adminUser = await userManager.FindByEmailAsync(adminEmail);
        if (adminUser == null) return;

        if (await db.LeagueUserMapping.AnyAsync(m => m.LeagueId == league.Id && m.UserId == adminUser.Id))
            return;

        db.LeagueUserMapping.Add(new LeagueUserMapping
        {
            LeagueId = league.Id,
            UserId = adminUser.Id,
        });
        await db.SaveChangesAsync();
        Log.Information("DemoDataSeeder: added admin to Demo League");
    }

    private static NflSpreads Spread(string home, string away, double homeSpread, double awaySpread, double ou, string gameTimeUtc) =>
        new()
        {
            Season = DemoSeason,
            NflWeek = DemoWeek,
            HomeTeam = home,
            AwayTeam = away,
            HomeTeamSpread = homeSpread,
            AwayTeamSpread = awaySpread,
            OverUnder = ou,
            GameTime = DateTime.Parse(gameTimeUtc, null, System.Globalization.DateTimeStyles.RoundtripKind),
        };
}
