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
    private const int DemoSeason = 2025;
    private const int DemoWeek = 18;

    // Fake demo users: name → email
    private static readonly (string Username, string Email)[] DemoUsers =
    [
        ("Alice",  "alice@demo.local"),
        ("Bob",    "bob@demo.local"),
        ("Carlos", "carlos@demo.local"),
        ("Dana",   "dana@demo.local"),
        ("Eve",    "eve@demo.local"),
    ];

    // Per-user picks for week 18: 4 picks each (matching required picks limit)
    // Games available: BUF/TB, DAL/LAR, GB/MIN, TEN/ATL, IND/NO, MIA/NE, NYG/NYJ, PIT/JAC,
    //                  WAS/PHI, CAR/HOU, SEA/CLE, DEN/KC, ARI/BAL, SF/CIN, LAC/CHI, DET/LV
    private static readonly Dictionary<string, string[]> DemoPicksMap = new()
    {
        ["Alice"]  = ["BUF", "DAL", "MIN", "MIA"],
        ["Bob"]    = ["TB",  "LAR", "GB",  "NE" ],
        ["Carlos"] = ["BUF", "DAL", "IND", "PHI"],
        ["Dana"]   = ["TB",  "LAR", "NO",  "MIA"],
        ["Eve"]    = ["BUF", "DAL", "MIN", "NE" ],
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
        await SeedHistoricalWeeksAsync(league);

        // CFB demo data
        var cfbLeague = await SeedCfbLeagueAsync();
        await SeedCfbLeagueMembersAsync(cfbLeague);
        var slates = await SeedCfbSlatesAsync();
        await SeedCfbSpreadsAsync(slates);
        await SeedCfbScoresAsync(slates);
        await SeedCfbPicksAsync(cfbLeague, slates);

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
            StartDate = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
            EndDate = new DateTimeOffset(2026, 1, 5, 23, 59, 59, TimeSpan.Zero),
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
            Spread(DemoWeek, "BUF", "TB",  -3.0,  3.0, 48.5, "2026-01-04T18:00:00Z"),
            Spread(DemoWeek, "DAL", "LAR", -6.5,  6.5, 46.5, "2026-01-04T18:00:00Z"),
            Spread(DemoWeek, "GB",  "MIN", -3.0,  3.0, 44.5, "2026-01-04T18:00:00Z"),
            Spread(DemoWeek, "TEN", "ATL",  1.5, -1.5, 38.5, "2026-01-04T18:00:00Z"),
            Spread(DemoWeek, "IND", "NO",  -2.5,  2.5, 41.5, "2026-01-04T18:00:00Z"),
            Spread(DemoWeek, "MIA", "NE",  -7.0,  7.0, 43.5, "2026-01-04T18:00:00Z"),
            Spread(DemoWeek, "NYG", "NYJ",  3.0, -3.0, 36.5, "2026-01-04T18:00:00Z"),
            Spread(DemoWeek, "PIT", "JAC", -1.0,  1.0, 40.5, "2026-01-04T18:00:00Z"),
            Spread(DemoWeek, "WAS", "PHI",  9.5, -9.5, 44.5, "2026-01-04T18:00:00Z"),
            Spread(DemoWeek, "CAR", "HOU",  3.5, -3.5, 39.5, "2026-01-04T18:00:00Z"),
            Spread(DemoWeek, "SEA", "CLE", -3.0,  3.0, 43.5, "2026-01-04T21:25:00Z"),
            Spread(DemoWeek, "DEN", "KC",  10.5,-10.5, 51.5, "2026-01-04T21:25:00Z"),
            Spread(DemoWeek, "ARI", "BAL", 13.5,-13.5, 44.5, "2026-01-04T21:25:00Z"),
            Spread(DemoWeek, "SF",  "CIN", -3.5,  3.5, 46.5, "2026-01-04T21:25:00Z"),
            Spread(DemoWeek, "LAC", "CHI", -7.0,  7.0, 41.5, "2026-01-05T01:20:00Z"),
            Spread(DemoWeek, "DET", "LV",  -7.5,  7.5, 47.5, "2026-01-05T01:15:00Z"),
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

        // 4 final games from frozen sample_espn_nfl.json for 2025 week 18
        var scores = new List<NflScores>
        {
            new() { Season = DemoSeason, NflWeek = DemoWeek, HomeTeam = "DAL", AwayTeam = "LAR", HomeTeamScore = 28, AwayTeamScore = 20, GameTime = new DateTimeOffset(2026, 1, 4, 18, 0, 0, TimeSpan.Zero) },
            new() { Season = DemoSeason, NflWeek = DemoWeek, HomeTeam = "GB",  AwayTeam = "MIN", HomeTeamScore = 17, AwayTeamScore = 24, GameTime = new DateTimeOffset(2026, 1, 4, 18, 0, 0, TimeSpan.Zero) },
            new() { Season = DemoSeason, NflWeek = DemoWeek, HomeTeam = "MIA", AwayTeam = "NE",  HomeTeamScore = 31, AwayTeamScore = 17, GameTime = new DateTimeOffset(2026, 1, 4, 18, 0, 0, TimeSpan.Zero) },
            new() { Season = DemoSeason, NflWeek = DemoWeek, HomeTeam = "WAS", AwayTeam = "PHI", HomeTeamScore = 7,  AwayTeamScore = 38, GameTime = new DateTimeOffset(2026, 1, 4, 18, 0, 0, TimeSpan.Zero) },
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

        for (int i = 0; i < picks.Length; i++)
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

    // Historical weeks 1-17: same 4 games every week, home teams always cover
    // Winning user picks: KC, DAL, PHI, BUF (all home = all cover)
    // Losing user picks:  DEN, DAL, PHI, BUF (DEN = wrong = loss)
    private static readonly string[] HistWinPicks = ["KC", "DAL", "PHI", "BUF"];
    private static readonly string[] HistLosePicks = ["DEN", "DAL", "PHI", "BUF"];

    // Win pattern per user per week (weeks 1-17, index 0-16); true = win that week
    // Weeks 8-17 repeat the weeks 1-7 pattern (cycled) for a plausible leaderboard.
    private static readonly Dictionary<string, bool[]> HistWinPatterns = new()
    {
        ["Alice"]  = [true,  true,  true,  true,  true,  true,  true,  true,  true,  true,  true,  true,  true,  true,  true,  true,  true],
        ["Bob"]    = [false, true,  false, false, true,  true,  false, false, true,  false, false, true,  true,  false, false, true,  false],
        ["Carlos"] = [true,  false, true,  false, true,  true,  true,  true,  false, true,  false, true,  true,  true,  true,  false, true],
        ["Dana"]   = [false, false, true,  false, true,  false, false, false, false, true,  false, true,  false, false, false, false, true],
        ["Eve"]    = [false, false, false, true,  true,  false, false, false, false, false, true,  true,  false, false, false, false, false],
    };

    // Wild Card (week 19): 6 games
    private static readonly (string Home, string Away, double HomeSpread, double AwaySpread, double OU, int HomeScore, int AwayScore, DateTimeOffset GameTime)[] WildCardGames =
    [
        ("KC",  "HOU", -6.5,  6.5, 48.5, 27, 14, new DateTimeOffset(2026, 1, 11, 18, 0, 0, TimeSpan.Zero)),
        ("BUF", "DEN", -7.5,  7.5, 46.5, 31,  7, new DateTimeOffset(2026, 1, 11, 21, 30, 0, TimeSpan.Zero)),
        ("BAL", "PIT", -8.0,  8.0, 44.5, 28, 14, new DateTimeOffset(2026, 1, 11, 21, 30, 0, TimeSpan.Zero)),
        ("PHI", "LAR", -9.5,  9.5, 47.5, 35, 14, new DateTimeOffset(2026, 1, 12, 18, 0, 0, TimeSpan.Zero)),
        ("DET", "WAS", -7.0,  7.0, 50.5, 24, 14, new DateTimeOffset(2026, 1, 12, 21, 30, 0, TimeSpan.Zero)),
        ("SF",  "GB",  -3.5,  3.5, 47.0, 21, 13, new DateTimeOffset(2026, 1, 12, 21, 30, 0, TimeSpan.Zero)),
    ];

    // Divisional (week 20): 4 games
    private static readonly (string Home, string Away, double HomeSpread, double AwaySpread, double OU, int HomeScore, int AwayScore, DateTimeOffset GameTime)[] DivisionalGames =
    [
        ("KC",  "BUF", -1.5,  1.5, 51.5, 24, 21, new DateTimeOffset(2026, 1, 18, 18, 0, 0, TimeSpan.Zero)),
        ("PHI", "DET", -3.0,  3.0, 48.0, 28, 24, new DateTimeOffset(2026, 1, 18, 21, 30, 0, TimeSpan.Zero)),
        ("BAL", "HOU", -4.5,  4.5, 47.0, 17, 13, new DateTimeOffset(2026, 1, 19, 18, 0, 0, TimeSpan.Zero)),
        ("SF",  "LAR", -5.5,  5.5, 46.0, 20, 13, new DateTimeOffset(2026, 1, 19, 21, 30, 0, TimeSpan.Zero)),
    ];

    // Conference Championship (week 21): 2 games
    private static readonly (string Home, string Away, double HomeSpread, double AwaySpread, double OU, int HomeScore, int AwayScore, DateTimeOffset GameTime)[] ConfChampGames =
    [
        ("KC",  "BAL", -2.5,  2.5, 47.5, 31, 24, new DateTimeOffset(2026, 1, 26, 18, 0, 0, TimeSpan.Zero)),
        ("PHI", "SF",  -2.0,  2.0, 45.5, 23, 13, new DateTimeOffset(2026, 1, 26, 21, 30, 0, TimeSpan.Zero)),
    ];

    // Super Bowl (week 22): 1 game
    private static readonly (string Home, string Away, double HomeSpread, double AwaySpread, double OU, int HomeScore, int AwayScore, DateTimeOffset GameTime)[] SuperBowlGames =
    [
        ("PHI", "KC",  -1.5,  1.5, 48.5, 38, 35, new DateTimeOffset(2026, 2, 9, 23, 30, 0, TimeSpan.Zero)),
    ];

    // Postseason picks per user (true = home team, false = away team)
    private static readonly Dictionary<string, bool[]> WildCardPicks = new()
    {
        ["Alice"]  = [true,  true,  true,  true,  true,  true],   // KC, BUF, BAL, PHI, DET, SF
        ["Bob"]    = [false, false, false, false, false, false],   // HOU, DEN, PIT, LAR, WAS, GB
        ["Carlos"] = [true,  true,  true,  true,  true,  false],  // KC, BUF, BAL, PHI, DET, GB
        ["Dana"]   = [false, false, false, false, false, true],   // HOU, DEN, PIT, LAR, WAS, SF
        ["Eve"]    = [true,  true,  true,  true,  true,  true],   // KC, BUF, BAL, PHI, DET, SF
    };

    private static readonly Dictionary<string, bool[]> DivisionalPicks = new()
    {
        ["Alice"]  = [true,  true,  true,  true],   // KC, PHI, BAL, SF
        ["Bob"]    = [false, false, false, false],  // BUF, DET, HOU, LAR
        ["Carlos"] = [true,  true,  true,  true],   // KC, PHI, BAL, SF
        ["Dana"]   = [false, false, false, false],  // BUF, DET, HOU, LAR
        ["Eve"]    = [true,  true,  true,  true],   // KC, PHI, BAL, SF
    };

    private static readonly Dictionary<string, bool[]> ConfChampPicks = new()
    {
        ["Alice"]  = [true,  true],   // KC, PHI
        ["Bob"]    = [false, false],  // BAL, SF
        ["Carlos"] = [true,  true],   // KC, PHI
        ["Dana"]   = [false, false],  // BAL, SF
        ["Eve"]    = [true,  true],   // KC, PHI
    };

    private static readonly Dictionary<string, bool> SuperBowlPicksMap = new()
    {
        ["Alice"]  = true,   // PHI
        ["Bob"]    = false,  // KC
        ["Carlos"] = true,   // PHI
        ["Dana"]   = false,  // KC
        ["Eve"]    = true,   // PHI
    };

    private async Task SeedHistoricalWeeksAsync(LeagueInfo? league)
    {
        if (league == null) return;
        if (await db.NflSpreads.AnyAsync(s => s.Season == DemoSeason && s.NflWeek == 1))
            return;

        var adminEmail = configuration["ADMIN_EMAIL"] ?? throw new InvalidOperationException("ADMIN_EMAIL required");
        var adminUser = await userManager.FindByEmailAsync(adminEmail);
        if (adminUser == null) return;

        // Admin (frizat) win pattern for weeks 1-17: W W L W W W W W W L W W W W W W W
        bool[] adminWins = [true, true, false, true, true, true, true, true, true, false, true, true, true, true, true, true, true];

        // Build user list
        var users = new List<(ApplicationUser User, bool[] Wins)> { (adminUser, adminWins) };
        foreach (var (username, _) in DemoUsers)
        {
            var u = await userManager.FindByNameAsync(username);
            if (u != null && HistWinPatterns.TryGetValue(username, out var pattern))
                users.Add((u, pattern));
        }

        // Seed regular season weeks 1-17
        for (int week = 1; week <= 17; week++)
        {
            var weekGameTime = new DateTimeOffset(2025, 9, 4, 17, 0, 0, TimeSpan.Zero).AddDays((week - 1) * 7 + 3);

            // NflWeeks
            if (!await db.NflWeeks.AnyAsync(w => w.Season == DemoSeason && w.NflWeek == week))
            {
                var weekStart = new DateTimeOffset(2025, 9, 4, 0, 0, 0, TimeSpan.Zero).AddDays((week - 1) * 7);
                db.NflWeeks.Add(new NflWeeks { Season = DemoSeason, NflWeek = week, StartDate = weekStart, EndDate = weekStart.AddDays(6) });
                await db.SaveChangesAsync();
            }
            var nflWeek = await db.NflWeeks.FirstAsync(w => w.Season == DemoSeason && w.NflWeek == week);

            // NflSpreads (4 games, home teams favored)
            db.NflSpreads.AddRange(
                Spread(week, "KC",  "DEN", -7.0,  7.0, 47.5, weekGameTime.ToString("o")),
                Spread(week, "DAL", "CLE", -6.0,  6.0, 44.5, weekGameTime.ToString("o")),
                Spread(week, "PHI", "NYG", -4.0,  4.0, 43.5, weekGameTime.ToString("o")),
                Spread(week, "BUF", "NYJ", -3.0,  3.0, 46.5, weekGameTime.ToString("o"))
            );

            // NflScores (all home teams win and cover)
            db.NflScores.AddRange(
                new NflScores { Season = DemoSeason, NflWeek = week, HomeTeam = "KC",  AwayTeam = "DEN", HomeTeamScore = 24, AwayTeamScore = 14, GameTime = weekGameTime },
                new NflScores { Season = DemoSeason, NflWeek = week, HomeTeam = "DAL", AwayTeam = "CLE", HomeTeamScore = 28, AwayTeamScore = 20, GameTime = weekGameTime },
                new NflScores { Season = DemoSeason, NflWeek = week, HomeTeam = "PHI", AwayTeam = "NYG", HomeTeamScore = 20, AwayTeamScore = 13, GameTime = weekGameTime },
                new NflScores { Season = DemoSeason, NflWeek = week, HomeTeam = "BUF", AwayTeam = "NYJ", HomeTeamScore = 17, AwayTeamScore = 10, GameTime = weekGameTime }
            );
            await db.SaveChangesAsync();

            // NflPicks
            int weekIdx = week - 1;
            foreach (var (user, wins) in users)
            {
                var picks = wins[weekIdx] ? HistWinPicks : HistLosePicks;
                foreach (var team in picks)
                {
                    db.NflPicks.Add(new NflPicks
                    {
                        UserId = user.Id, LeagueId = league.Id, Team = team,
                        Pick = PickType.Spread, NflWeek = week, Season = DemoSeason,
                        NflWeekId = nflWeek.Id, DateCreated = DateTimeOffset.UtcNow,
                    });
                }
            }
            await db.SaveChangesAsync();
        }

        // Seed postseason weeks (19-22)
        await SeedPostseasonWeekAsync(league, users, 19, "Wild Card",
            new DateTimeOffset(2026, 1, 11, 0, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 1, 12, 23, 59, 59, TimeSpan.Zero),
            WildCardGames.Select(g => (g.Home, g.Away, g.HomeSpread, g.AwaySpread, g.OU, g.HomeScore, g.AwayScore, g.GameTime)).ToArray(),
            WildCardPicks);

        await SeedPostseasonWeekAsync(league, users, 20, "Divisional",
            new DateTimeOffset(2026, 1, 18, 0, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 1, 19, 23, 59, 59, TimeSpan.Zero),
            DivisionalGames.Select(g => (g.Home, g.Away, g.HomeSpread, g.AwaySpread, g.OU, g.HomeScore, g.AwayScore, g.GameTime)).ToArray(),
            DivisionalPicks);

        await SeedPostseasonWeekAsync(league, users, 21, "Conference Championship",
            new DateTimeOffset(2026, 1, 26, 0, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 1, 26, 23, 59, 59, TimeSpan.Zero),
            ConfChampGames.Select(g => (g.Home, g.Away, g.HomeSpread, g.AwaySpread, g.OU, g.HomeScore, g.AwayScore, g.GameTime)).ToArray(),
            ConfChampPicks);

        // Super Bowl — build picks dict from SuperBowlPicksMap (bool→bool[])
        var sbPicksAsArrays = SuperBowlPicksMap.ToDictionary(kv => kv.Key, kv => new[] { kv.Value });
        await SeedPostseasonWeekAsync(league, users, 22, "Super Bowl",
            new DateTimeOffset(2026, 2, 9, 0, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 2, 9, 23, 59, 59, TimeSpan.Zero),
            SuperBowlGames.Select(g => (g.Home, g.Away, g.HomeSpread, g.AwaySpread, g.OU, g.HomeScore, g.AwayScore, g.GameTime)).ToArray(),
            sbPicksAsArrays);

        Log.Information("DemoDataSeeder: seeded historical weeks 1-17 + postseason (19-22) for {UserCount} users", users.Count);
    }

    private async Task SeedPostseasonWeekAsync(
        LeagueInfo league,
        List<(ApplicationUser User, bool[] Wins)> users,
        int week,
        string label,
        DateTimeOffset startDate,
        DateTimeOffset endDate,
        (string Home, string Away, double HomeSpread, double AwaySpread, double OU, int HomeScore, int AwayScore, DateTimeOffset GameTime)[] games,
        Dictionary<string, bool[]> pickPatterns)
    {
        // NflWeeks row
        if (!await db.NflWeeks.AnyAsync(w => w.Season == DemoSeason && w.NflWeek == week))
        {
            db.NflWeeks.Add(new NflWeeks { Season = DemoSeason, NflWeek = week, StartDate = startDate, EndDate = endDate });
            await db.SaveChangesAsync();
        }
        var nflWeek = await db.NflWeeks.FirstAsync(w => w.Season == DemoSeason && w.NflWeek == week);

        // Spreads
        foreach (var g in games)
            db.NflSpreads.Add(Spread(week, g.Home, g.Away, g.HomeSpread, g.AwaySpread, g.OU, g.GameTime.ToString("o")));

        // Scores
        foreach (var g in games)
            db.NflScores.Add(new NflScores { Season = DemoSeason, NflWeek = week, HomeTeam = g.Home, AwayTeam = g.Away, HomeTeamScore = g.HomeScore, AwayTeamScore = g.AwayScore, GameTime = g.GameTime });

        await db.SaveChangesAsync();

        // Picks — admin uses same pattern as Alice for postseason
        var allPickers = new List<(ApplicationUser User, string Name)>();
        allPickers.Add((users[0].User, users[0].User.UserName!)); // admin
        foreach (var (username, _) in DemoUsers)
        {
            var u = users.FirstOrDefault(x => x.User.UserName == username);
            if (u.User != null) allPickers.Add((u.User, username));
        }

        foreach (var (user, name) in allPickers)
        {
            // Admin uses Alice's pattern
            var patternKey = pickPatterns.ContainsKey(name) ? name : "Alice";
            if (!pickPatterns.TryGetValue(patternKey, out var pattern)) continue;

            for (int i = 0; i < games.Length; i++)
            {
                var team = (i < pattern.Length && pattern[i]) ? games[i].Home : games[i].Away;
                db.NflPicks.Add(new NflPicks
                {
                    UserId = user.Id, LeagueId = league.Id, Team = team,
                    Pick = PickType.Spread, NflWeek = week, Season = DemoSeason,
                    NflWeekId = nflWeek.Id, DateCreated = DateTimeOffset.UtcNow,
                });
            }
        }
        await db.SaveChangesAsync();
        Log.Information("DemoDataSeeder: seeded postseason week {Week} ({Label})", week, label);
    }

    private static NflSpreads Spread(int week, string home, string away, double homeSpread, double awaySpread, double ou, string gameTimeUtc) =>
        new()
        {
            Season = DemoSeason,
            NflWeek = week,
            HomeTeam = home,
            AwayTeam = away,
            HomeTeamSpread = homeSpread,
            AwayTeamSpread = awaySpread,
            OverUnder = ou,
            // Parse as UTC explicitly — DateTimeOffset.Parse preserves offset, avoiding local-time conversion
            GameTime = DateTimeOffset.Parse(gameTimeUtc, null, System.Globalization.DateTimeStyles.AssumeUniversal | System.Globalization.DateTimeStyles.AdjustToUniversal),
        };

    // -----------------------------------------------------------------------
    // CFB Demo Seeding
    // -----------------------------------------------------------------------

    private const int CfbDemoSeason = 2025;

    // Regular season weeks 1-7
    private static readonly (int SlateIdx, int EventId, string Home, string Away, double HomeSpread, double AwaySpread, double OU, int HomeScore, int AwayScore, DateTimeOffset GameTime)[] Slate1Games =
    [
        (1, 401700001, "MICH", "ILL",  -14.0, 14.0, 52.5, 45, 17, new DateTimeOffset(2025,  8, 30, 17,  0, 0, TimeSpan.Zero)),
        (1, 401700002, "OSU",  "AKR",  -38.5, 38.5, 62.5, 56, 14, new DateTimeOffset(2025,  8, 30, 17,  0, 0, TimeSpan.Zero)),
        (1, 401700003, "UGA",  "CLEM",  -7.0,  7.0, 51.0, 28, 21, new DateTimeOffset(2025,  8, 30, 20,  0, 0, TimeSpan.Zero)),
        (1, 401700004, "ALA",  "MISS",  -3.5,  3.5, 53.5, 24, 21, new DateTimeOffset(2025,  8, 30, 20,  0, 0, TimeSpan.Zero)),
        (1, 401700005, "ORE",  "USC",   -9.5,  9.5, 56.5, 34, 20, new DateTimeOffset(2025,  8, 30, 22, 30, 0, TimeSpan.Zero)),
        (1, 401700006, "ND",   "TAMU",  -4.0,  4.0, 49.5, 28, 24, new DateTimeOffset(2025,  8, 30, 22, 30, 0, TimeSpan.Zero)),
    ];

    private static readonly (int SlateIdx, int EventId, string Home, string Away, double HomeSpread, double AwaySpread, double OU, int HomeScore, int AwayScore, DateTimeOffset GameTime)[] Slate2Games =
    [
        (2, 401700011, "MICH", "ARK",  -17.5, 17.5, 55.5, 35, 14, new DateTimeOffset(2025,  9,  6, 17,  0, 0, TimeSpan.Zero)),
        (2, 401700012, "ALA",  "ND",    -5.5,  5.5, 54.0, 31, 28, new DateTimeOffset(2025,  9,  6, 20,  0, 0, TimeSpan.Zero)),
        (2, 401700013, "OSU",  "TAMU",  -7.0,  7.0, 58.5, 38, 24, new DateTimeOffset(2025,  9,  6, 20,  0, 0, TimeSpan.Zero)),
        (2, 401700014, "UGA",  "SC",   -10.5, 10.5, 52.5, 34, 17, new DateTimeOffset(2025,  9,  6, 20,  0, 0, TimeSpan.Zero)),
        (2, 401700015, "ORE",  "MISS",  -6.5,  6.5, 59.5, 42, 35, new DateTimeOffset(2025,  9,  6, 22, 30, 0, TimeSpan.Zero)),
        (2, 401700016, "CLEM", "FSU",   -7.0,  7.0, 48.5, 28, 17, new DateTimeOffset(2025,  9,  6, 22, 30, 0, TimeSpan.Zero)),
    ];

    private static readonly (int SlateIdx, int EventId, string Home, string Away, double HomeSpread, double AwaySpread, double OU, int HomeScore, int AwayScore, DateTimeOffset GameTime)[] Slate3Games =
    [
        (3, 401700021, "MICH", "TENN",  -3.5,  3.5, 51.5, 21, 17, new DateTimeOffset(2025,  9, 13, 17,  0, 0, TimeSpan.Zero)),
        (3, 401700022, "OSU",  "ND",    -5.5,  5.5, 56.0, 35, 28, new DateTimeOffset(2025,  9, 13, 17,  0, 0, TimeSpan.Zero)),
        (3, 401700023, "ALA",  "LSU",   -3.0,  3.0, 55.0, 34, 27, new DateTimeOffset(2025,  9, 13, 20,  0, 0, TimeSpan.Zero)),
        (3, 401700024, "UGA",  "AUB",   -9.5,  9.5, 50.5, 31, 14, new DateTimeOffset(2025,  9, 13, 20,  0, 0, TimeSpan.Zero)),
        (3, 401700025, "ORE",  "WASH",  -7.5,  7.5, 55.5, 38, 21, new DateTimeOffset(2025,  9, 13, 22, 30, 0, TimeSpan.Zero)),
        (3, 401700026, "CLEM", "WIS",   -6.5,  6.5, 48.0, 24, 14, new DateTimeOffset(2025,  9, 13, 22, 30, 0, TimeSpan.Zero)),
    ];

    private static readonly (int SlateIdx, int EventId, string Home, string Away, double HomeSpread, double AwaySpread, double OU, int HomeScore, int AwayScore, DateTimeOffset GameTime)[] Slate4Games =
    [
        (4, 401700031, "MICH", "PSU",  -3.5,  3.5, 46.5, 24, 21, new DateTimeOffset(2025,  9, 20, 17,  0, 0, TimeSpan.Zero)),
        (4, 401700032, "OSU",  "IU",  -13.5, 13.5, 58.5, 45, 28, new DateTimeOffset(2025,  9, 20, 17,  0, 0, TimeSpan.Zero)),
        (4, 401700033, "UGA",  "OU",   -7.0,  7.0, 57.5, 35, 21, new DateTimeOffset(2025,  9, 20, 20,  0, 0, TimeSpan.Zero)),
        (4, 401700034, "ALA",  "SC",  -10.0, 10.0, 52.0, 28, 14, new DateTimeOffset(2025,  9, 20, 20,  0, 0, TimeSpan.Zero)),
        (4, 401700035, "ORE",  "UCLA", -9.5,  9.5, 56.0, 38, 28, new DateTimeOffset(2025,  9, 20, 22, 30, 0, TimeSpan.Zero)),
        (4, 401700036, "CLEM", "NC",   -7.5,  7.5, 46.5, 28, 17, new DateTimeOffset(2025,  9, 20, 22, 30, 0, TimeSpan.Zero)),
    ];

    private static readonly (int SlateIdx, int EventId, string Home, string Away, double HomeSpread, double AwaySpread, double OU, int HomeScore, int AwayScore, DateTimeOffset GameTime)[] Slate5Games =
    [
        (5, 401700041, "OSU",  "MICH", -4.0,  4.0, 48.5, 31, 27, new DateTimeOffset(2025,  9, 27, 17,  0, 0, TimeSpan.Zero)),
        (5, 401700042, "UGA",  "MISS", -7.5,  7.5, 58.5, 38, 28, new DateTimeOffset(2025,  9, 27, 17,  0, 0, TimeSpan.Zero)),
        (5, 401700043, "ALA",  "TAMU", -5.5,  5.5, 55.0, 31, 24, new DateTimeOffset(2025,  9, 27, 20,  0, 0, TimeSpan.Zero)),
        (5, 401700044, "LSU",  "ND",   -2.5,  2.5, 56.5, 28, 21, new DateTimeOffset(2025,  9, 27, 20,  0, 0, TimeSpan.Zero)),
        (5, 401700045, "ORE",  "UTAH", -7.0,  7.0, 57.5, 35, 21, new DateTimeOffset(2025,  9, 27, 22, 30, 0, TimeSpan.Zero)),
        (5, 401700046, "CLEM", "GT",  -14.5, 14.5, 49.5, 35, 17, new DateTimeOffset(2025,  9, 27, 22, 30, 0, TimeSpan.Zero)),
    ];

    private static readonly (int SlateIdx, int EventId, string Home, string Away, double HomeSpread, double AwaySpread, double OU, int HomeScore, int AwayScore, DateTimeOffset GameTime)[] Slate6Games =
    [
        (6, 401700051, "MICH", "MIN",  -7.5,  7.5, 48.5, 24, 14, new DateTimeOffset(2025, 10,  4, 17,  0, 0, TimeSpan.Zero)),
        (6, 401700052, "OSU",  "ORE",  -2.5,  2.5, 58.5, 35, 28, new DateTimeOffset(2025, 10,  4, 17,  0, 0, TimeSpan.Zero)),
        (6, 401700053, "ALA",  "CLEM", -6.5,  6.5, 54.0, 28, 21, new DateTimeOffset(2025, 10,  4, 20,  0, 0, TimeSpan.Zero)),
        (6, 401700054, "UGA",  "LSU",  -3.5,  3.5, 55.5, 27, 24, new DateTimeOffset(2025, 10,  4, 20,  0, 0, TimeSpan.Zero)),
        (6, 401700055, "ND",   "FSU",  -7.5,  7.5, 52.0, 31, 21, new DateTimeOffset(2025, 10,  4, 22, 30, 0, TimeSpan.Zero)),
        (6, 401700056, "TAMU", "MISS", -2.5,  2.5, 58.5, 35, 31, new DateTimeOffset(2025, 10,  4, 22, 30, 0, TimeSpan.Zero)),
    ];

    private static readonly (int SlateIdx, int EventId, string Home, string Away, double HomeSpread, double AwaySpread, double OU, int HomeScore, int AwayScore, DateTimeOffset GameTime)[] Slate7Games =
    [
        (7, 401700061, "MICH", "NEB",  -10.5, 10.5, 52.5, 34, 17, new DateTimeOffset(2025, 10, 11, 17,  0, 0, TimeSpan.Zero)),
        (7, 401700062, "OSU",  "TENN",  -6.0,  6.0, 56.5, 35, 24, new DateTimeOffset(2025, 10, 11, 17,  0, 0, TimeSpan.Zero)),
        (7, 401700063, "UGA",  "CLEM",  -7.5,  7.5, 50.5, 31, 21, new DateTimeOffset(2025, 10, 11, 20,  0, 0, TimeSpan.Zero)),
        (7, 401700064, "ALA",  "ORE",   -3.5,  3.5, 57.5, 28, 24, new DateTimeOffset(2025, 10, 11, 20,  0, 0, TimeSpan.Zero)),
        (7, 401700065, "ND",   "SC",    -8.5,  8.5, 52.5, 31, 17, new DateTimeOffset(2025, 10, 11, 22, 30, 0, TimeSpan.Zero)),
        (7, 401700066, "LSU",  "TAMU",  -3.0,  3.0, 52.5, 24, 21, new DateTimeOffset(2025, 10, 11, 22, 30, 0, TimeSpan.Zero)),
    ];

    // 2025 CFB Week 8 Top 25 matchups (real games, all final)
    private static readonly (int SlateIdx, int EventId, string Home, string Away, double HomeSpread, double AwaySpread, double OU, int HomeScore, int AwayScore, DateTimeOffset GameTime)[] Week8Games =
    [
        (8, 401700101, "MICH",  "PSU",   -3.5,  3.5, 44.5, 27, 13, new DateTimeOffset(2025, 10, 11, 20,  0, 0, TimeSpan.Zero)),
        (8, 401700102, "ALA",   "TENN",  -7.0,  7.0, 51.5, 24, 17, new DateTimeOffset(2025, 10, 11, 20,  0, 0, TimeSpan.Zero)),
        (8, 401700103, "OSU",   "ORE",   -2.5,  2.5, 56.0, 32, 31, new DateTimeOffset(2025, 10, 11, 23, 30, 0, TimeSpan.Zero)),
        (8, 401700104, "UGA",   "MIA",   -6.5,  6.5, 53.0, 31, 14, new DateTimeOffset(2025, 10, 11, 23, 30, 0, TimeSpan.Zero)),
        (8, 401700105, "LSU",   "TAMU",  -3.0,  3.0, 48.5, 21, 17, new DateTimeOffset(2025, 10, 11, 20,  0, 0, TimeSpan.Zero)),
        (8, 401700106, "CLEM",  "FSU",   -7.5,  7.5, 46.5, 35, 14, new DateTimeOffset(2025, 10, 11, 17, 30, 0, TimeSpan.Zero)),
    ];

    // Regular season weeks 9-14
    private static readonly (int SlateIdx, int EventId, string Home, string Away, double HomeSpread, double AwaySpread, double OU, int HomeScore, int AwayScore, DateTimeOffset GameTime)[] Slate9Games =
    [
        (9, 401700111, "MICH", "WIS",   -7.5,  7.5, 50.5, 28, 14, new DateTimeOffset(2025, 10, 18, 17,  0, 0, TimeSpan.Zero)),
        (9, 401700112, "OSU",  "ALA",   -3.5,  3.5, 58.5, 31, 24, new DateTimeOffset(2025, 10, 18, 17,  0, 0, TimeSpan.Zero)),
        (9, 401700113, "UGA",  "TAMU",  -9.5,  9.5, 54.0, 35, 21, new DateTimeOffset(2025, 10, 18, 20,  0, 0, TimeSpan.Zero)),
        (9, 401700114, "ND",   "MISS",  -4.5,  4.5, 56.5, 28, 21, new DateTimeOffset(2025, 10, 18, 20,  0, 0, TimeSpan.Zero)),
        (9, 401700115, "ORE",  "CLEM",  -3.0,  3.0, 54.5, 24, 21, new DateTimeOffset(2025, 10, 18, 22, 30, 0, TimeSpan.Zero)),
        (9, 401700116, "LSU",  "FSU",   -6.5,  6.5, 55.0, 34, 21, new DateTimeOffset(2025, 10, 18, 22, 30, 0, TimeSpan.Zero)),
    ];

    private static readonly (int SlateIdx, int EventId, string Home, string Away, double HomeSpread, double AwaySpread, double OU, int HomeScore, int AwayScore, DateTimeOffset GameTime)[] Slate10Games =
    [
        (10, 401700121, "MICH", "IOWA", -14.5, 14.5, 52.0, 38, 17, new DateTimeOffset(2025, 10, 25, 17,  0, 0, TimeSpan.Zero)),
        (10, 401700122, "OSU",  "ILL",  -21.5, 21.5, 58.5, 52, 14, new DateTimeOffset(2025, 10, 25, 17,  0, 0, TimeSpan.Zero)),
        (10, 401700123, "UGA",  "ND",    -7.5,  7.5, 54.5, 28, 21, new DateTimeOffset(2025, 10, 25, 20,  0, 0, TimeSpan.Zero)),
        (10, 401700124, "ALA",  "MISS",  -3.0,  3.0, 58.5, 35, 28, new DateTimeOffset(2025, 10, 25, 20,  0, 0, TimeSpan.Zero)),
        (10, 401700125, "ORE",  "UTAH",  -9.5,  9.5, 57.5, 38, 24, new DateTimeOffset(2025, 10, 25, 22, 30, 0, TimeSpan.Zero)),
        (10, 401700126, "TAMU", "CLEM",  -2.5,  2.5, 54.0, 28, 24, new DateTimeOffset(2025, 10, 25, 22, 30, 0, TimeSpan.Zero)),
    ];

    private static readonly (int SlateIdx, int EventId, string Home, string Away, double HomeSpread, double AwaySpread, double OU, int HomeScore, int AwayScore, DateTimeOffset GameTime)[] Slate11Games =
    [
        (11, 401700131, "MICH", "OSU",    4.5, -4.5, 51.5, 24, 31, new DateTimeOffset(2025, 11,  1, 17,  0, 0, TimeSpan.Zero)),
        (11, 401700132, "ALA",  "LSU",   -3.0,  3.0, 58.5, 31, 24, new DateTimeOffset(2025, 11,  1, 17,  0, 0, TimeSpan.Zero)),
        (11, 401700133, "UGA",  "MISS", -10.5, 10.5, 59.5, 38, 17, new DateTimeOffset(2025, 11,  1, 20,  0, 0, TimeSpan.Zero)),
        (11, 401700134, "ND",   "CLEM",  -6.5,  6.5, 52.5, 28, 20, new DateTimeOffset(2025, 11,  1, 20,  0, 0, TimeSpan.Zero)),
        (11, 401700135, "ORE",  "TAMU",  -5.5,  5.5, 58.5, 35, 28, new DateTimeOffset(2025, 11,  1, 22, 30, 0, TimeSpan.Zero)),
        (11, 401700136, "IU",   "NEB",   -7.5,  7.5, 54.5, 31, 21, new DateTimeOffset(2025, 11,  1, 22, 30, 0, TimeSpan.Zero)),
    ];

    private static readonly (int SlateIdx, int EventId, string Home, string Away, double HomeSpread, double AwaySpread, double OU, int HomeScore, int AwayScore, DateTimeOffset GameTime)[] Slate12Games =
    [
        (12, 401700141, "OSU",  "MICH", -3.5,  3.5, 53.5, 28, 24, new DateTimeOffset(2025, 11,  8, 17,  0, 0, TimeSpan.Zero)),
        (12, 401700142, "ALA",  "SC",  -11.5, 11.5, 52.5, 35, 17, new DateTimeOffset(2025, 11,  8, 17,  0, 0, TimeSpan.Zero)),
        (12, 401700143, "UGA",  "TENN", -8.5,  8.5, 54.0, 31, 17, new DateTimeOffset(2025, 11,  8, 20,  0, 0, TimeSpan.Zero)),
        (12, 401700144, "ND",   "PITT", -9.5,  9.5, 52.0, 35, 21, new DateTimeOffset(2025, 11,  8, 20,  0, 0, TimeSpan.Zero)),
        (12, 401700145, "ORE",  "UCLA", -9.0,  9.0, 56.5, 38, 24, new DateTimeOffset(2025, 11,  8, 22, 30, 0, TimeSpan.Zero)),
        (12, 401700146, "IU",   "PU",   -5.5,  5.5, 53.5, 28, 21, new DateTimeOffset(2025, 11,  8, 22, 30, 0, TimeSpan.Zero)),
    ];

    private static readonly (int SlateIdx, int EventId, string Home, string Away, double HomeSpread, double AwaySpread, double OU, int HomeScore, int AwayScore, DateTimeOffset GameTime)[] Slate13Games =
    [
        (13, 401700151, "OSU",  "UGA",  -2.5,  2.5, 54.5, 31, 28, new DateTimeOffset(2025, 11, 15, 17,  0, 0, TimeSpan.Zero)),
        (13, 401700152, "ALA",  "TAMU", -5.5,  5.5, 57.5, 35, 24, new DateTimeOffset(2025, 11, 15, 17,  0, 0, TimeSpan.Zero)),
        (13, 401700153, "ND",   "MICH", -3.5,  3.5, 52.5, 28, 21, new DateTimeOffset(2025, 11, 15, 20,  0, 0, TimeSpan.Zero)),
        (13, 401700154, "ORE",  "IU",   -7.5,  7.5, 57.0, 35, 28, new DateTimeOffset(2025, 11, 15, 20,  0, 0, TimeSpan.Zero)),
        (13, 401700155, "CLEM", "SC",   -3.5,  3.5, 50.5, 24, 17, new DateTimeOffset(2025, 11, 15, 22, 30, 0, TimeSpan.Zero)),
        (13, 401700156, "LSU",  "MISS", -3.0,  3.0, 62.0, 35, 28, new DateTimeOffset(2025, 11, 15, 22, 30, 0, TimeSpan.Zero)),
    ];

    private static readonly (int SlateIdx, int EventId, string Home, string Away, double HomeSpread, double AwaySpread, double OU, int HomeScore, int AwayScore, DateTimeOffset GameTime)[] Slate14Games =
    [
        (14, 401700161, "OSU",  "MICH", -5.5,  5.5, 52.5, 34, 28, new DateTimeOffset(2025, 11, 22, 17,  0, 0, TimeSpan.Zero)),
        (14, 401700162, "ALA",  "AUB",  -6.5,  6.5, 54.0, 24, 17, new DateTimeOffset(2025, 11, 22, 17,  0, 0, TimeSpan.Zero)),
        (14, 401700163, "UGA",  "GT",  -17.5, 17.5, 57.0, 38, 14, new DateTimeOffset(2025, 11, 22, 20,  0, 0, TimeSpan.Zero)),
        (14, 401700164, "ND",   "SC",   -8.0,  8.0, 52.5, 28, 17, new DateTimeOffset(2025, 11, 22, 20,  0, 0, TimeSpan.Zero)),
        (14, 401700165, "ORE",  "WASH", -8.5,  8.5, 57.5, 35, 21, new DateTimeOffset(2025, 11, 22, 22, 30, 0, TimeSpan.Zero)),
        (14, 401700166, "IU",   "MIN",  -7.5,  7.5, 55.5, 31, 21, new DateTimeOffset(2025, 11, 22, 22, 30, 0, TimeSpan.Zero)),
    ];

    // Conference Championships (slate 15)
    private static readonly (int SlateIdx, int EventId, string Home, string Away, double HomeSpread, double AwaySpread, double OU, int HomeScore, int AwayScore, DateTimeOffset GameTime)[] Slate15Games =
    [
        (15, 401700201, "OSU",  "IU",   -6.5,  6.5, 56.5, 34, 31, new DateTimeOffset(2025, 12,  6, 17,  0, 0, TimeSpan.Zero)),
        (15, 401700202, "ALA",  "UGA",  -3.5,  3.5, 54.5, 24, 17, new DateTimeOffset(2025, 12,  6, 20,  0, 0, TimeSpan.Zero)),
        (15, 401700203, "ND",   "CLEM", -5.5,  5.5, 51.5, 28, 21, new DateTimeOffset(2025, 12,  6, 22, 30, 0, TimeSpan.Zero)),
        (15, 401700204, "ORE",  "BOIS", -9.5,  9.5, 54.0, 35, 21, new DateTimeOffset(2025, 12,  6, 20,  0, 0, TimeSpan.Zero)),
        (15, 401700205, "KSU",  "OU",   -3.0,  3.0, 51.0, 24, 17, new DateTimeOffset(2025, 12,  6, 16,  0, 0, TimeSpan.Zero)),
        (15, 401700206, "MISS", "TAMU", -2.5,  2.5, 56.5, 28, 24, new DateTimeOffset(2025, 12,  7, 17,  0, 0, TimeSpan.Zero)),
    ];

    // 2025 CFP matchups (real bracket, all final as of Jan 2026)
    // SlateIdx now refers to SlateNumber (16=First Round, 17=QF, 18=SF, 19=Championship)
    private static readonly (int SlateIdx, int EventId, string Home, string Away, double HomeSpread, double AwaySpread, double OU, int HomeScore, int AwayScore, DateTimeOffset GameTime)[] CfpGames =
    [
        // Slate 16: First Round (Dec 19-20)
        (16, 401800001, "ORE",  "JMU",  -24.5, 24.5, 52.5, 38, 10, new DateTimeOffset(2025, 12, 19, 20,  0, 0, TimeSpan.Zero)),
        (16, 401800002, "MISS", "TULN", -17.5, 17.5, 58.0, 35, 17, new DateTimeOffset(2025, 12, 19, 23, 30, 0, TimeSpan.Zero)),
        (16, 401800003, "TAMU", "MIA",   -7.0,  7.0, 49.5, 24, 17, new DateTimeOffset(2025, 12, 20, 20,  0, 0, TimeSpan.Zero)),
        (16, 401800004, "OU",   "ALA",   -3.0,  3.0, 51.0, 21, 14, new DateTimeOffset(2025, 12, 20, 23, 30, 0, TimeSpan.Zero)),
        // Slate 17: Quarterfinals (Dec 31/Jan 1)
        (17, 401800005, "IU",   "ALA",   -3.5,  3.5, 48.5, 27, 24, new DateTimeOffset(2026,  1,  1, 17,  0, 0, TimeSpan.Zero)),
        (17, 401800006, "UGA",  "MISS", -10.0, 10.0, 55.0, 35, 21, new DateTimeOffset(2026,  1,  1, 20, 30, 0, TimeSpan.Zero)),
        (17, 401800007, "ORE",  "TTU",   -3.5,  3.5, 53.0, 31, 20, new DateTimeOffset(2025, 12, 31, 20,  0, 0, TimeSpan.Zero)),
        (17, 401800008, "MIA",  "OSU",    7.0, -7.0, 56.5, 28, 24, new DateTimeOffset(2025, 12, 31, 23, 30, 0, TimeSpan.Zero)),
        // Slate 18: Semifinals (Jan 8-9)
        (18, 401800009, "IU",   "ORE",   -3.0,  3.0, 51.5, 34, 27, new DateTimeOffset(2026,  1,  9, 20,  0, 0, TimeSpan.Zero)),
        (18, 401800010, "MIA",  "UGA",    3.5, -3.5, 50.0, 21, 17, new DateTimeOffset(2026,  1,  8, 20,  0, 0, TimeSpan.Zero)),
        // Slate 19: Championship (Jan 19)
        (19, 401800011, "IU",   "MIA",   -3.0,  3.0, 46.5, 23, 20, new DateTimeOffset(2026,  1, 19, 23, 30, 0, TimeSpan.Zero)),
    ];

    private async Task<LeagueInfo?> SeedCfbLeagueAsync()
    {
        var existing = await db.LeagueInfo.FirstOrDefaultAsync(l => l.LeagueName == "CFB Demo League");
        if (existing != null) return existing;

        var adminEmail = configuration["ADMIN_EMAIL"] ?? throw new InvalidOperationException("ADMIN_EMAIL required");
        var adminUser = await userManager.FindByEmailAsync(adminEmail);
        if (adminUser == null) return null;

        var league = new LeagueInfo { LeagueName = "CFB Demo League", OwnerUserId = adminUser.Id, LeagueType = LeagueType.Cfb };
        db.LeagueInfo.Add(league);
        await db.SaveChangesAsync();
        Log.Information("DemoDataSeeder: created CFB Demo League (id={Id})", league.Id);
        return league;
    }

    private async Task SeedCfbLeagueMembersAsync(LeagueInfo? league)
    {
        if (league == null) return;

        var adminEmail = configuration["ADMIN_EMAIL"] ?? throw new InvalidOperationException("ADMIN_EMAIL required");
        var adminUser = await userManager.FindByEmailAsync(adminEmail);
        if (adminUser != null && !await db.LeagueUserMapping.AnyAsync(m => m.LeagueId == league.Id && m.UserId == adminUser.Id))
        {
            db.LeagueUserMapping.Add(new LeagueUserMapping { LeagueId = league.Id, UserId = adminUser.Id });
            await db.SaveChangesAsync();
        }

        foreach (var (_, email) in DemoUsers)
        {
            var user = await userManager.FindByEmailAsync(email);
            if (user == null) continue;
            if (!await db.LeagueUserMapping.AnyAsync(m => m.LeagueId == league.Id && m.UserId == user.Id))
            {
                db.LeagueUserMapping.Add(new LeagueUserMapping { LeagueId = league.Id, UserId = user.Id });
                await db.SaveChangesAsync();
            }
        }
        Log.Information("DemoDataSeeder: added all demo users to CFB Demo League");
    }

    private const int CfbExpectedSlateCount = 19;

    private async Task<List<CfbSlates>> SeedCfbSlatesAsync()
    {
        var existing = await db.CfbSlates.Where(s => s.Season == CfbDemoSeason).ToListAsync();
        if (existing.Count >= CfbExpectedSlateCount) return existing;

        // Remove stale partial seed and ALL dependent data (picks included) before re-seeding
        if (existing.Count > 0) {
            var staleIds = existing.Select(s => s.Id).ToList();
            db.CfbPicks.RemoveRange(db.CfbPicks.Where(p => staleIds.Contains(p.CfbSlateId)));
            db.CfbScores.RemoveRange(db.CfbScores.Where(s => staleIds.Contains(s.CfbSlateId)));
            db.CfbSpreads.RemoveRange(db.CfbSpreads.Where(s => staleIds.Contains(s.CfbSlateId)));
            db.CfbSlates.RemoveRange(existing);
            await db.SaveChangesAsync();
        }

        var slates = new List<CfbSlates>
        {
            // Regular season weeks 1-14
            new() { Season = CfbDemoSeason, SlateNumber =  1, Label = "Week 1",  SlateType = "RegularSeason",          StartDate = new DateOnly(2025,  8, 23), EndDate = new DateOnly(2025,  8, 30) },
            new() { Season = CfbDemoSeason, SlateNumber =  2, Label = "Week 2",  SlateType = "RegularSeason",          StartDate = new DateOnly(2025,  8, 30), EndDate = new DateOnly(2025,  9,  6) },
            new() { Season = CfbDemoSeason, SlateNumber =  3, Label = "Week 3",  SlateType = "RegularSeason",          StartDate = new DateOnly(2025,  9,  6), EndDate = new DateOnly(2025,  9, 13) },
            new() { Season = CfbDemoSeason, SlateNumber =  4, Label = "Week 4",  SlateType = "RegularSeason",          StartDate = new DateOnly(2025,  9, 13), EndDate = new DateOnly(2025,  9, 20) },
            new() { Season = CfbDemoSeason, SlateNumber =  5, Label = "Week 5",  SlateType = "RegularSeason",          StartDate = new DateOnly(2025,  9, 20), EndDate = new DateOnly(2025,  9, 27) },
            new() { Season = CfbDemoSeason, SlateNumber =  6, Label = "Week 6",  SlateType = "RegularSeason",          StartDate = new DateOnly(2025,  9, 27), EndDate = new DateOnly(2025, 10,  4) },
            new() { Season = CfbDemoSeason, SlateNumber =  7, Label = "Week 7",  SlateType = "RegularSeason",          StartDate = new DateOnly(2025, 10,  4), EndDate = new DateOnly(2025, 10, 11) },
            new() { Season = CfbDemoSeason, SlateNumber =  8, Label = "Week 8",  SlateType = "RegularSeason",          StartDate = new DateOnly(2025, 10, 11), EndDate = new DateOnly(2025, 10, 18) },
            new() { Season = CfbDemoSeason, SlateNumber =  9, Label = "Week 9",  SlateType = "RegularSeason",          StartDate = new DateOnly(2025, 10, 18), EndDate = new DateOnly(2025, 10, 25) },
            new() { Season = CfbDemoSeason, SlateNumber = 10, Label = "Week 10", SlateType = "RegularSeason",          StartDate = new DateOnly(2025, 10, 25), EndDate = new DateOnly(2025, 11,  1) },
            new() { Season = CfbDemoSeason, SlateNumber = 11, Label = "Week 11", SlateType = "RegularSeason",          StartDate = new DateOnly(2025, 11,  1), EndDate = new DateOnly(2025, 11,  8) },
            new() { Season = CfbDemoSeason, SlateNumber = 12, Label = "Week 12", SlateType = "RegularSeason",          StartDate = new DateOnly(2025, 11,  8), EndDate = new DateOnly(2025, 11, 15) },
            new() { Season = CfbDemoSeason, SlateNumber = 13, Label = "Week 13", SlateType = "RegularSeason",          StartDate = new DateOnly(2025, 11, 15), EndDate = new DateOnly(2025, 11, 22) },
            new() { Season = CfbDemoSeason, SlateNumber = 14, Label = "Week 14", SlateType = "RegularSeason",          StartDate = new DateOnly(2025, 11, 22), EndDate = new DateOnly(2025, 11, 29) },
            // Conference Championship Week
            new() { Season = CfbDemoSeason, SlateNumber = 15, Label = "Conf. Championships", SlateType = "ConferenceChampionship", StartDate = new DateOnly(2025, 12,  5), EndDate = new DateOnly(2025, 12,  7) },
            // CFP Postseason
            new() { Season = CfbDemoSeason, SlateNumber = 16, Label = "CFP First Round",           SlateType = "FirstRound",   StartDate = new DateOnly(2025, 12, 19), EndDate = new DateOnly(2025, 12, 20) },
            new() { Season = CfbDemoSeason, SlateNumber = 17, Label = "CFP Quarterfinals",         SlateType = "Quarterfinal", StartDate = new DateOnly(2025, 12, 31), EndDate = new DateOnly(2026,  1,  1) },
            new() { Season = CfbDemoSeason, SlateNumber = 18, Label = "CFP Semifinals",            SlateType = "Semifinal",    StartDate = new DateOnly(2026,  1,  8), EndDate = new DateOnly(2026,  1,  9) },
            new() { Season = CfbDemoSeason, SlateNumber = 19, Label = "CFP National Championship", SlateType = "Championship", StartDate = new DateOnly(2026,  1, 19), EndDate = new DateOnly(2026,  1, 19) },
        };
        db.CfbSlates.AddRange(slates);
        await db.SaveChangesAsync();
        Log.Information("DemoDataSeeder: seeded {Count} CFB slates for {Season}", slates.Count, CfbDemoSeason);
        return slates;
    }

    private async Task SeedCfbSpreadsAsync(List<CfbSlates> slates)
    {
        if (await db.CfbSpreads.AnyAsync(s => slates.Select(sl => sl.Id).Contains(s.CfbSlateId)))
            return;

        var allGames = Slate1Games
            .Concat(Slate2Games)
            .Concat(Slate3Games)
            .Concat(Slate4Games)
            .Concat(Slate5Games)
            .Concat(Slate6Games)
            .Concat(Slate7Games)
            .Concat(Week8Games)
            .Concat(Slate9Games)
            .Concat(Slate10Games)
            .Concat(Slate11Games)
            .Concat(Slate12Games)
            .Concat(Slate13Games)
            .Concat(Slate14Games)
            .Concat(Slate15Games)
            .Concat(CfpGames);

        var spreads = allGames.Select(g => new CfbSpreads
        {
            CfbSlateId     = slates.First(s => s.SlateNumber == g.SlateIdx).Id,
            EspnEventId    = g.EventId,
            HomeTeam       = g.Home,
            AwayTeam       = g.Away,
            HomeTeamSpread = g.HomeSpread,
            AwayTeamSpread = g.AwaySpread,
            OverUnder      = g.OU,
            GameTime       = g.GameTime,
        }).ToList();

        db.CfbSpreads.AddRange(spreads);
        await db.SaveChangesAsync();
        Log.Information("DemoDataSeeder: seeded {Count} CFB spreads (all slates)", spreads.Count);
    }

    private async Task SeedCfbScoresAsync(List<CfbSlates> slates)
    {
        if (await db.CfbScores.AnyAsync(s => slates.Select(sl => sl.Id).Contains(s.CfbSlateId)))
            return;

        var allGames = Slate1Games
            .Concat(Slate2Games)
            .Concat(Slate3Games)
            .Concat(Slate4Games)
            .Concat(Slate5Games)
            .Concat(Slate6Games)
            .Concat(Slate7Games)
            .Concat(Week8Games)
            .Concat(Slate9Games)
            .Concat(Slate10Games)
            .Concat(Slate11Games)
            .Concat(Slate12Games)
            .Concat(Slate13Games)
            .Concat(Slate14Games)
            .Concat(Slate15Games)
            .Concat(CfpGames);

        var scores = allGames.Select(g => new CfbScores
        {
            CfbSlateId    = slates.First(s => s.SlateNumber == g.SlateIdx).Id,
            EspnEventId   = g.EventId,
            HomeTeam      = g.Home,
            AwayTeam      = g.Away,
            HomeTeamScore = g.HomeScore,
            AwayTeamScore = g.AwayScore,
            GameStatus    = "StatusFinal",
            GameTime      = g.GameTime,
        }).ToList();

        db.CfbScores.AddRange(scores);
        await db.SaveChangesAsync();
        Log.Information("DemoDataSeeder: seeded {Count} CFB scores (all slates, all final)", scores.Count);
    }

    // CFB pick patterns — true = home team, false = away team
    // Alice: always home (favorites)
    // Bob: always away (underdogs)
    // Carlos: home for games 1,3,5 and away for 2,4,6
    // Dana: away for games 1,3,5 and home for 2,4,6
    // Eve: home for games 1,2,4 and away for 3,5,6
    private static readonly Dictionary<string, bool[]> CfbRegularSeasonPickPattern = new()
    {
        ["Alice"]  = [true,  true,  true,  true,  true,  true],
        ["Bob"]    = [false, false, false, false, false, false],
        ["Carlos"] = [true,  false, true,  false, true,  false],
        ["Dana"]   = [false, true,  false, true,  false, true],
        ["Eve"]    = [true,  true,  false, true,  false, false],
    };

    // Who picks which home team (true) or away team (false) per game index, per slate
    // Week 8 games: MICH/PSU, ALA/TENN, OSU/ORE, UGA/MIA, LSU/TAMU, CLEM/FSU
    // CFP QF:       IU/ALA,   UGA/MISS, ORE/TTU,  MIA/OSU
    // CFP SF:       IU/ORE,   MIA/UGA
    // CFP Final:    IU/MIA
    private static readonly Dictionary<string, bool[]> CfbWeek8Picks = new()
    {
        ["Alice"]  = [true,  true,  true,  true,  true,  true],  // all favorites
        ["Bob"]    = [false, false, false, false, false, false], // all underdogs
        ["Carlos"] = [true,  false, true,  false, true,  true],
        ["Dana"]   = [false, true,  false, true,  false, false],
        ["Eve"]    = [true,  true,  false, true,  true,  false],
    };

    private static readonly Dictionary<string, bool[]> CfbQfPicks = new()
    {
        ["Alice"]  = [true,  true,  true,  false], // IU, UGA, ORE, OSU
        ["Bob"]    = [false, false, false, true],  // ALA, MISS, TTU, MIA
        ["Carlos"] = [true,  false, true,  false], // IU, MISS, ORE, OSU
        ["Dana"]   = [false, true,  false, true],  // ALA, UGA, TTU, MIA
        ["Eve"]    = [true,  true,  false, false], // IU, UGA, TTU, OSU
    };

    private static readonly Dictionary<string, bool[]> CfbSfPicks = new()
    {
        ["Alice"]  = [true,  false], // IU, UGA
        ["Bob"]    = [false, true],  // ORE, MIA
        ["Carlos"] = [true,  true],  // IU, MIA
        ["Dana"]   = [false, false], // ORE, UGA
        ["Eve"]    = [true,  false], // IU, UGA
    };

    private static readonly Dictionary<string, bool> CfbFinalPicks = new()
    {
        ["Alice"]  = true,   // IU
        ["Bob"]    = false,  // MIA
        ["Carlos"] = true,   // IU
        ["Dana"]   = false,  // MIA
        ["Eve"]    = true,   // IU
    };

    private async Task SeedCfbPicksAsync(LeagueInfo? league, List<CfbSlates> slates)
    {
        if (league == null) return;
        // 5 users × 101 picks each:
        // Slates 1-7: 7×6=42, Slate 8: 6, Slates 9-14: 6×6=36, Slate 15: 6
        // Slate 16 (FR): 4, Slate 17 (QF): 4, Slate 18 (SF): 2, Slate 19 (Champ): 1
        // Total per user: 42+6+36+6+4+4+2+1 = 101 → 5×101 = 505
        const int ExpectedPickCount = 505;
        if (await db.CfbPicks.CountAsync(p => p.LeagueId == league.Id) >= ExpectedPickCount) return;
        // Clear any partial seed before re-seeding
        db.CfbPicks.RemoveRange(db.CfbPicks.Where(p => p.LeagueId == league.Id));
        await db.SaveChangesAsync();

        var picks = new List<CfbPicks>();

        void AddPick(int leagueId, string userId, int slateId, int eventId, string team) =>
            picks.Add(new CfbPicks { UserId = userId, LeagueId = leagueId, CfbSlateId = slateId, EspnEventId = eventId, Team = team, PickType = "Spread", Season = CfbDemoSeason });

        // Helper to get games for a given slate number from a static array
        static IEnumerable<(int SlateIdx, int EventId, string Home, string Away, double HomeSpread, double AwaySpread, double OU, int HomeScore, int AwayScore, DateTimeOffset GameTime)>
            GamesForSlate(
                (int SlateIdx, int EventId, string Home, string Away, double HomeSpread, double AwaySpread, double OU, int HomeScore, int AwayScore, DateTimeOffset GameTime)[] arr,
                int slateNum) =>
            arr.Where(g => g.SlateIdx == slateNum);

        foreach (var (username, _) in DemoUsers)
        {
            var user = await userManager.FindByNameAsync(username);
            if (user == null) continue;

            // Regular season slates 1-7 (use CfbRegularSeasonPickPattern)
            if (CfbRegularSeasonPickPattern.TryGetValue(username, out var rsPattern))
            {
                var regularSlates = new (int SlateNum, (int SlateIdx, int EventId, string Home, string Away, double HomeSpread, double AwaySpread, double OU, int HomeScore, int AwayScore, DateTimeOffset GameTime)[])[]
                {
                    (1, Slate1Games), (2, Slate2Games), (3, Slate3Games), (4, Slate4Games),
                    (5, Slate5Games), (6, Slate6Games), (7, Slate7Games),
                };
                foreach (var (slateNum, gamesArr) in regularSlates)
                {
                    if (slates.FirstOrDefault(s => s.SlateNumber == slateNum) is not { } slate) continue;
                    var gamesForSlate = gamesArr; // all 6 games for this slate
                    for (int i = 0; i < gamesForSlate.Length; i++)
                    {
                        var g = gamesForSlate[i];
                        var pickHome = i < rsPattern.Length && rsPattern[i];
                        AddPick(league.Id, user.Id, slate.Id, g.EventId, pickHome ? g.Home : g.Away);
                    }
                }
            }

            // Week 8 (use CfbWeek8Picks)
            if (CfbWeek8Picks.TryGetValue(username, out var w8) && slates.FirstOrDefault(s => s.SlateNumber == 8) is { } slate8)
                for (int i = 0; i < Week8Games.Length; i++)
                    AddPick(league.Id, user.Id, slate8.Id, Week8Games[i].EventId, w8[i] ? Week8Games[i].Home : Week8Games[i].Away);

            // Regular season slates 9-14 (use CfbRegularSeasonPickPattern)
            if (CfbRegularSeasonPickPattern.TryGetValue(username, out var rsPattern2))
            {
                var regularSlates9to14 = new (int SlateNum, (int SlateIdx, int EventId, string Home, string Away, double HomeSpread, double AwaySpread, double OU, int HomeScore, int AwayScore, DateTimeOffset GameTime)[])[]
                {
                    (9, Slate9Games), (10, Slate10Games), (11, Slate11Games),
                    (12, Slate12Games), (13, Slate13Games), (14, Slate14Games),
                };
                foreach (var (slateNum, gamesArr) in regularSlates9to14)
                {
                    if (slates.FirstOrDefault(s => s.SlateNumber == slateNum) is not { } slate) continue;
                    for (int i = 0; i < gamesArr.Length; i++)
                    {
                        var g = gamesArr[i];
                        var pickHome = i < rsPattern2.Length && rsPattern2[i];
                        AddPick(league.Id, user.Id, slate.Id, g.EventId, pickHome ? g.Home : g.Away);
                    }
                }
            }

            // Slate 15: Conference Championships (use CfbRegularSeasonPickPattern)
            if (CfbRegularSeasonPickPattern.TryGetValue(username, out var confPattern) && slates.FirstOrDefault(s => s.SlateNumber == 15) is { } slate15)
                for (int i = 0; i < Slate15Games.Length; i++)
                {
                    var g = Slate15Games[i];
                    var pickHome = i < confPattern.Length && confPattern[i];
                    AddPick(league.Id, user.Id, slate15.Id, g.EventId, pickHome ? g.Home : g.Away);
                }

            // CFP First Round (slate 16) — use regular season pattern
            if (CfbRegularSeasonPickPattern.TryGetValue(username, out var fr16Pattern) && slates.FirstOrDefault(s => s.SlateNumber == 16) is { } slate16)
            {
                var fr16Games = CfpGames.Where(g => g.SlateIdx == 16).ToArray();
                for (int i = 0; i < fr16Games.Length; i++)
                {
                    var g = fr16Games[i];
                    var pickHome = i < fr16Pattern.Length && fr16Pattern[i];
                    AddPick(league.Id, user.Id, slate16.Id, g.EventId, pickHome ? g.Home : g.Away);
                }
            }

            // CFP Quarterfinals (slate 17)
            if (CfbQfPicks.TryGetValue(username, out var qf) && slates.FirstOrDefault(s => s.SlateNumber == 17) is { } slateQf)
            {
                var qfGames = CfpGames.Where(g => g.SlateIdx == 17).ToArray();
                for (int i = 0; i < qfGames.Length; i++)
                    AddPick(league.Id, user.Id, slateQf.Id, qfGames[i].EventId, qf[i] ? qfGames[i].Home : qfGames[i].Away);
            }

            // CFP Semifinals (slate 18)
            if (CfbSfPicks.TryGetValue(username, out var sf) && slates.FirstOrDefault(s => s.SlateNumber == 18) is { } slateSf)
            {
                var sfGames = CfpGames.Where(g => g.SlateIdx == 18).ToArray();
                for (int i = 0; i < sfGames.Length; i++)
                    AddPick(league.Id, user.Id, slateSf.Id, sfGames[i].EventId, sf[i] ? sfGames[i].Home : sfGames[i].Away);
            }

            // CFP Championship (slate 19)
            if (CfbFinalPicks.TryGetValue(username, out var final) && slates.FirstOrDefault(s => s.SlateNumber == 19) is { } slateFinal)
            {
                var finalGame = CfpGames.First(g => g.SlateIdx == 19);
                AddPick(league.Id, user.Id, slateFinal.Id, finalGame.EventId, final ? finalGame.Home : finalGame.Away);
            }
        }

        if (picks.Count > 0)
        {
            db.CfbPicks.AddRange(picks);
            await db.SaveChangesAsync();
            Log.Information("DemoDataSeeder: seeded {Count} CFB picks", picks.Count);
        }
    }
}
