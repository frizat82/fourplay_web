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

    // Per-user picks for week 8: 4 picks each (matching required picks limit)
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

    // Historical weeks 1-7: same 4 games every week, home teams always cover
    // Winning user picks: KC, DAL, PHI, BUF (all home = all cover)
    // Losing user picks:  DEN, DAL, PHI, BUF (DEN = wrong = loss)
    private static readonly string[] HistWinPicks = ["KC", "DAL", "PHI", "BUF"];
    private static readonly string[] HistLosePicks = ["DEN", "DAL", "PHI", "BUF"];

    // Win pattern per user per week (weeks 1-7, index 0-6); true = win that week
    // Scoring: WeeklyCost=5. Week 5 = all win → carryover → WeeklyCost=10 for week 6.
    // Result: Alice +95, frizat +65, Carlos +35, Bob -25, Dana/Eve -85
    private static readonly Dictionary<string, bool[]> HistWinPatterns = new()
    {
        ["Alice"]  = [true,  true,  true,  true,  true, true,  true],
        ["Bob"]    = [false, true,  false, false, true, true,  false],
        ["Carlos"] = [true,  false, true,  false, true, true,  true],
        ["Dana"]   = [false, false, true,  false, true, false, false],
        ["Eve"]    = [false, false, false, true,  true, false, false],
    };

    private async Task SeedHistoricalWeeksAsync(LeagueInfo? league)
    {
        if (league == null) return;
        if (await db.NflSpreads.AnyAsync(s => s.Season == DemoSeason && s.NflWeek == 1))
            return;

        var adminEmail = configuration["ADMIN_EMAIL"] ?? throw new InvalidOperationException("ADMIN_EMAIL required");
        var adminUser = await userManager.FindByEmailAsync(adminEmail);
        if (adminUser == null) return;

        // Admin (frizat) win pattern: W W L W W W W
        bool[] adminWins = [true, true, false, true, true, true, true];

        // Build user list
        var users = new List<(ApplicationUser User, bool[] Wins)> { (adminUser, adminWins) };
        foreach (var (username, _) in DemoUsers)
        {
            var u = await userManager.FindByNameAsync(username);
            if (u != null && HistWinPatterns.TryGetValue(username, out var pattern))
                users.Add((u, pattern));
        }

        for (int week = 1; week <= 7; week++)
        {
            var weekGameTime = new DateTimeOffset(2023, 9, 4, 17, 0, 0, TimeSpan.Zero).AddDays((week - 1) * 7 + 3);

            // NflWeeks
            if (!await db.NflWeeks.AnyAsync(w => w.Season == DemoSeason && w.NflWeek == week))
            {
                var weekStart = new DateTimeOffset(2023, 9, 4, 0, 0, 0, TimeSpan.Zero).AddDays((week - 1) * 7);
                db.NflWeeks.Add(new NflWeeks { Season = DemoSeason, NflWeek = week, StartDate = weekStart, EndDate = weekStart.AddDays(6) });
                await db.SaveChangesAsync();
            }
            var nflWeek = await db.NflWeeks.FirstAsync(w => w.Season == DemoSeason && w.NflWeek == week);

            // NflSpreads (4 games, home teams favored)
            db.NflSpreads.AddRange(
                new NflSpreads { Season = DemoSeason, NflWeek = week, HomeTeam = "KC",  AwayTeam = "DEN", HomeTeamSpread = -7.0, AwayTeamSpread = 7.0, OverUnder = 47.5, GameTime = weekGameTime },
                new NflSpreads { Season = DemoSeason, NflWeek = week, HomeTeam = "DAL", AwayTeam = "CLE", HomeTeamSpread = -6.0, AwayTeamSpread = 6.0, OverUnder = 44.5, GameTime = weekGameTime },
                new NflSpreads { Season = DemoSeason, NflWeek = week, HomeTeam = "PHI", AwayTeam = "NYG", HomeTeamSpread = -4.0, AwayTeamSpread = 4.0, OverUnder = 43.5, GameTime = weekGameTime },
                new NflSpreads { Season = DemoSeason, NflWeek = week, HomeTeam = "BUF", AwayTeam = "NYJ", HomeTeamSpread = -3.0, AwayTeamSpread = 3.0, OverUnder = 46.5, GameTime = weekGameTime }
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

        Log.Information("DemoDataSeeder: seeded historical weeks 1-7 for {UserCount} users", users.Count);
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

    // -----------------------------------------------------------------------
    // CFB Demo Seeding
    // -----------------------------------------------------------------------

    private const int CfbDemoSeason = 2025;

    // 2025 CFB Week 8 Top 25 matchups (real games, all final)
    private static readonly (int SlateIdx, int EventId, string Home, string Away, double HomeSpread, double AwaySpread, double OU, int HomeScore, int AwayScore, DateTimeOffset GameTime)[] Week8Games =
    [
        (8, 401700101, "MICH",  "PSU",   -3.5,  3.5, 44.5, 27, 13, new DateTimeOffset(2025, 10, 11, 20, 0, 0, TimeSpan.Zero)),
        (8, 401700102, "ALA",   "TENN",  -7.0,  7.0, 51.5, 24, 17, new DateTimeOffset(2025, 10, 11, 20, 0, 0, TimeSpan.Zero)),
        (8, 401700103, "OSU",   "ORE",   -2.5,  2.5, 56.0, 32, 31, new DateTimeOffset(2025, 10, 11, 23, 30, 0, TimeSpan.Zero)),
        (8, 401700104, "UGA",   "MIA",   -6.5,  6.5, 53.0, 31, 14, new DateTimeOffset(2025, 10, 11, 23, 30, 0, TimeSpan.Zero)),
        (8, 401700105, "LSU",   "TAMU",  -3.0,  3.0, 48.5, 21, 17, new DateTimeOffset(2025, 10, 11, 20, 0, 0, TimeSpan.Zero)),
        (8, 401700106, "CLEM",  "FSU",   -7.5,  7.5, 46.5, 35, 14, new DateTimeOffset(2025, 10, 11, 17, 30, 0, TimeSpan.Zero)),
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

        var allGames = Week8Games.Concat(CfpGames);
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
        Log.Information("DemoDataSeeder: seeded {Count} CFB spreads (week 8 + CFP)", spreads.Count);
    }

    private async Task SeedCfbScoresAsync(List<CfbSlates> slates)
    {
        if (await db.CfbScores.AnyAsync(s => slates.Select(sl => sl.Id).Contains(s.CfbSlateId)))
            return;

        var allGames = Week8Games.Concat(CfpGames);
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
        Log.Information("DemoDataSeeder: seeded {Count} CFB scores (week 8 + CFP, all final)", scores.Count);
    }

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
        if (await db.CfbPicks.AnyAsync(p => p.LeagueId == league.Id)) return;

        var picks = new List<CfbPicks>();

        void AddPick(int leagueId, string userId, int slateId, int eventId, string team) =>
            picks.Add(new CfbPicks { UserId = userId, LeagueId = leagueId, CfbSlateId = slateId, EspnEventId = eventId, Team = team, PickType = "Spread", Season = CfbDemoSeason });

        foreach (var (username, _) in DemoUsers)
        {
            var user = await userManager.FindByNameAsync(username);
            if (user == null) continue;

            // Week 8
            if (CfbWeek8Picks.TryGetValue(username, out var w8) && slates.FirstOrDefault(s => s.SlateNumber == 8) is { } slate8)
                for (int i = 0; i < Week8Games.Length; i++)
                    AddPick(league.Id, user.Id, slate8.Id, Week8Games[i].EventId, w8[i] ? Week8Games[i].Home : Week8Games[i].Away);

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
