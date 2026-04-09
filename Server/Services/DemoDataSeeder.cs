using FourPlayWebApp.Server.Data;
using FourPlayWebApp.Server.Models.Data;
using FourPlayWebApp.Server.Models.Identity;
using FourPlayWebApp.Shared.Models.Data;
using FourPlayWebApp.Shared.Models.Enum;
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

    // Fake demo users: name → email
    private static readonly (string Username, string Email)[] DemoUsers =
    [
        ("Alice",  "alice@demo.local"),
        ("Bob",    "bob@demo.local"),
        ("Carlos", "carlos@demo.local"),
        ("Dana",   "dana@demo.local"),
        ("Eve",    "eve@demo.local"),
    ];

    // Per-user picks: keyed by username, value is list of team abbreviations (one per game, in game order)
    // Games: BUF/TB, DAL/LAR, GB/MIN, TEN/ATL, IND/NO, MIA/NE, NYG/NYJ, PIT/JAC, WAS/PHI,
    //        CAR/HOU, SEA/CLE, DEN/KC, ARI/BAL, SF/CIN, LAC/CHI, DET/LV
    private static readonly Dictionary<string, string[]> DemoPicksMap = new()
    {
        ["Alice"]  = ["BUF","DAL","MIN","ATL","IND","MIA","NYJ","JAC","PHI","HOU","SEA","KC", "BAL","SF", "LAC","DET"],
        ["Bob"]    = ["TB", "LAR","MIN","ATL","NO", "NE", "NYG","PIT","WAS","CAR","CLE","DEN","ARI","CIN","CHI","LV" ],
        ["Carlos"] = ["BUF","DAL","GB", "TEN","IND","MIA","NYG","PIT","PHI","CAR","SEA","KC", "BAL","CIN","LAC","DET"],
        ["Dana"]   = ["TB", "LAR","GB", "ATL","NO", "MIA","NYJ","JAC","WAS","HOU","CLE","DEN","ARI","SF", "CHI","LV" ],
        ["Eve"]    = ["BUF","DAL","MIN","TEN","IND","NE", "NYG","JAC","PHI","CAR","SEA","KC", "BAL","SF", "LAC","LV" ],
    };

    // Games in order matching DemoPicksMap columns (mapped abbreviations, home first)
    private static readonly (string Home, string Away)[] DemoGames =
    [
        ("BUF","TB"),  ("DAL","LAR"), ("GB","MIN"),  ("TEN","ATL"), ("IND","NO"),
        ("MIA","NE"),  ("NYG","NYJ"), ("PIT","JAC"), ("WAS","PHI"), ("CAR","HOU"),
        ("SEA","CLE"), ("DEN","KC"),  ("ARI","BAL"), ("SF","CIN"),  ("LAC","CHI"),
        ("DET","LV"),
    ];

    public async Task SeedAsync()
    {
        Log.Information("DemoDataSeeder: starting seed for season {Season} week {Week}", DemoSeason, DemoWeek);

        await SeedNflWeekAsync();
        await SeedSpreadsAsync();
        var league = await SeedLeagueAsync();
        await SeedLeagueMemberAsync(league);
        await SeedLeagueJuiceMappingAsync(league);
        await SeedNflScoresAsync();
        await SeedDemoUsersAsync(league);

        Log.Information("DemoDataSeeder: seed complete");
    }

    private async Task SeedLeagueJuiceMappingAsync(LeagueInfo? league)
    {
        if (league == null) return;

        if (await db.LeagueJuiceMapping.AnyAsync(m => m.LeagueId == league.Id && m.Season == DemoSeason))
            return;

        db.LeagueJuiceMapping.Add(new LeagueJuiceMapping
        {
            LeagueId = league.Id,
            Season = DemoSeason,
            Juice = 13,
            JuiceDivisional = 10,
            JuiceConference = 6,
            WeeklyCost = 5,
            DateCreated = DateTimeOffset.UtcNow,
        });
        await db.SaveChangesAsync();
        Log.Information("DemoDataSeeder: created LeagueJuiceMapping for Demo League season {Season}", DemoSeason);
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
            // Fix any spreads seeded before the ESPN abbreviation mapping was applied
            await FixSpreadAbbreviationsAsync();
            var count = await db.NflSpreads.CountAsync(s => s.Season == DemoSeason && s.NflWeek == DemoWeek);
            Log.Information("DemoDataSeeder: spreads already seeded ({Count} rows)", count);
            return;
        }

        var spreads = new List<NflSpreads>
        {
            // Abbreviations match ESPN mapped values (WAS not WSH, JAC not JAX)
            Spread("BUF", "TB",  -3.0,  3.0, 48.5, "2023-10-27T00:15:00Z"),
            Spread("DAL", "LAR", -6.5,  6.5, 46.5, "2023-10-29T17:00:00Z"),
            Spread("GB",  "MIN", -3.0,  3.0, 44.5, "2023-10-29T17:00:00Z"),
            Spread("TEN", "ATL",  1.5, -1.5, 38.5, "2023-10-29T17:00:00Z"),
            Spread("IND", "NO",  -2.5,  2.5, 41.5, "2023-10-29T17:00:00Z"),
            Spread("MIA", "NE",  -7.0,  7.0, 43.5, "2023-10-29T17:00:00Z"),
            Spread("NYG", "NYJ",  3.0, -3.0, 36.5, "2023-10-29T17:00:00Z"),
            Spread("PIT", "JAC", -1.0,  1.0, 40.5, "2023-10-29T17:00:00Z"),
            Spread("WAS", "PHI",  9.5, -9.5, 44.5, "2023-10-29T17:00:00Z"),
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

    private async Task FixSpreadAbbreviationsAsync()
    {
        // Correct legacy abbreviations that were seeded before the ESPN mapping was applied
        var fixes = new Dictionary<string, string> { ["WSH"] = "WAS", ["JAX"] = "JAC", ["ARZ"] = "ARI" };
        bool changed = false;
        foreach (var spread in await db.NflSpreads.Where(s => s.Season == DemoSeason && s.NflWeek == DemoWeek).ToListAsync())
        {
            if (fixes.TryGetValue(spread.HomeTeam, out var fixedHome)) { spread.HomeTeam = fixedHome; changed = true; }
            if (fixes.TryGetValue(spread.AwayTeam, out var fixedAway)) { spread.AwayTeam = fixedAway; changed = true; }
        }
        if (changed) await db.SaveChangesAsync();
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

        var league = new LeagueInfo { LeagueName = "Demo League", OwnerUserId = adminUser.Id };
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

        db.LeagueUserMapping.Add(new LeagueUserMapping { LeagueId = league.Id, UserId = adminUser.Id });
        await db.SaveChangesAsync();
        Log.Information("DemoDataSeeder: added admin to Demo League");
    }

    private async Task SeedNflScoresAsync()
    {
        if (await db.NflScores.AnyAsync(s => s.Season == DemoSeason && s.NflWeek == DemoWeek))
            return;

        // 4 final games from frozen sample_espn_nfl.json for 2023 week 8
        var scores = new List<NflScores>
        {
            new() { Season = DemoSeason, NflWeek = DemoWeek, HomeTeam = "DAL", AwayTeam = "LAR", HomeTeamScore = 28, AwayTeamScore = 20, GameTime = new DateTimeOffset(2023, 10, 29, 17, 0, 0, TimeSpan.Zero) },
            new() { Season = DemoSeason, NflWeek = DemoWeek, HomeTeam = "GB",  AwayTeam = "MIN", HomeTeamScore = 17, AwayTeamScore = 24, GameTime = new DateTimeOffset(2023, 10, 29, 17, 0, 0, TimeSpan.Zero) },
            new() { Season = DemoSeason, NflWeek = DemoWeek, HomeTeam = "MIA", AwayTeam = "NE",  HomeTeamScore = 31, AwayTeamScore = 17, GameTime = new DateTimeOffset(2023, 10, 29, 17, 0, 0, TimeSpan.Zero) },
            new() { Season = DemoSeason, NflWeek = DemoWeek, HomeTeam = "WAS", AwayTeam = "PHI", HomeTeamScore = 7,  AwayTeamScore = 38, GameTime = new DateTimeOffset(2023, 10, 29, 17, 0, 0, TimeSpan.Zero) },
        };
        db.NflScores.AddRange(scores);
        await db.SaveChangesAsync();
        Log.Information("DemoDataSeeder: seeded {Count} final NflScores for week {Week}", scores.Count, DemoWeek);
    }

    private async Task SeedDemoUsersAsync(LeagueInfo? league)
    {
        if (league == null) return;

        var nflWeek = await db.NflWeeks.FirstOrDefaultAsync(w => w.Season == DemoSeason && w.NflWeek == DemoWeek);
        if (nflWeek == null) return;

        foreach (var (username, email) in DemoUsers)
        {
            var user = await EnsureDemoUserAsync(username, email, league);
            if (user == null) continue;
            await SeedPicksForUserAsync(user, league, nflWeek);
        }
    }

    private async Task<ApplicationUser?> EnsureDemoUserAsync(string username, string email, LeagueInfo league)
    {
        var user = await userManager.FindByEmailAsync(email);
        if (user == null)
        {
            user = new ApplicationUser { UserName = username, Email = email, EmailConfirmed = true };
            var result = await userManager.CreateAsync(user, "DemoPass@123");
            if (!result.Succeeded)
            {
                Log.Warning("DemoDataSeeder: failed to create demo user {Username}: {Errors}",
                    username, string.Join(", ", result.Errors.Select(e => e.Description)));
                return null;
            }
            Log.Information("DemoDataSeeder: created demo user {Username}", username);
        }

        // Ensure LeagueUsers record
        if (!await db.LeagueUsers.AnyAsync(lu => lu.Email == email))
        {
            db.LeagueUsers.Add(new LeagueUsers { Email = email });
            await db.SaveChangesAsync();
        }

        // Ensure league membership
        if (!await db.LeagueUserMapping.AnyAsync(m => m.LeagueId == league.Id && m.UserId == user.Id))
        {
            db.LeagueUserMapping.Add(new LeagueUserMapping { LeagueId = league.Id, UserId = user.Id });
            await db.SaveChangesAsync();
        }

        return user;
    }

    private async Task SeedPicksForUserAsync(ApplicationUser user, LeagueInfo league, NflWeeks nflWeek)
    {
        if (!DemoPicksMap.TryGetValue(user.UserName!, out var picks)) return;

        for (int i = 0; i < DemoGames.Length; i++)
        {
            var team = picks[i];
            var alreadyExists = await db.NflPicks.AnyAsync(p =>
                p.UserId == user.Id && p.LeagueId == league.Id &&
                p.Season == DemoSeason && p.NflWeek == DemoWeek && p.Team == team);
            if (alreadyExists) continue;

            db.NflPicks.Add(new NflPicks
            {
                UserId = user.Id,
                LeagueId = league.Id,
                Team = team,
                Pick = PickType.Spread,
                NflWeek = DemoWeek,
                Season = DemoSeason,
                NflWeekId = nflWeek.Id,
                DateCreated = DateTimeOffset.UtcNow,
            });
        }
        await db.SaveChangesAsync();
        Log.Information("DemoDataSeeder: seeded picks for {Username}", user.UserName);
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
