using FourPlayWebApp.Server.Data;
using FourPlayWebApp.Server.Models.Data;
using FourPlayWebApp.Server.Models.Identity;
using FourPlayWebApp.Server.Services.Interfaces;
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
public class DemoDataSeeder(
    ApplicationDbContext db,
    UserManager<ApplicationUser> userManager,
    IConfiguration configuration)
{
    private const int DemoSeason = 2025;
    private const int DemoWeek = 18;

    // frizat-703.6: the replayed game's own embedded season/week (see sample_espn_nfl_*.json).
    // The frontend derives "which week to show" entirely from the replay data's own season/week
    // fields, not from any separately-resolved "current week" — so the seeded spread must live
    // in this same week. Overridden to postseason ESPN week 5 (Super Bowl, NflWeek 22) rather
    // than the real game's original regular-season week: Super Bowl requires only 1 pick, so the
    // E2E spec can pick a single team and submit without needing 4 games in one fixture. Season is
    // bumped to 2026 (not the demo dataset's 2025) — NflWeek 22/2025 already has a full seeded
    // Super Bowl slate (NE @ SEA with picks for every user), which would collide with this game.
    private const int ReplaySeason = 2026;
    private const int ReplayWeek = 22;

    // CFB side of the same replay game (frizat-703.6): unlike NFL, there is no unused (season,
    // slate) slot to isolate into — CfbCurrentSlateService only ever resolves season 2026 (all
    // future, never picked as current before the real 2026 season starts) or its 2025 fallback,
    // and 2025's SlateNumbers 1-18 are already fully seeded with real spreads/picks for every demo
    // user. Worse, the WeekYearSelector clamps any (week, isPostSeason) pair outside its configured
    // range back to the last valid option, so an invented out-of-range slate number (e.g. 19) gets
    // silently redirected to the real Championship slate anyway. So: reuse the REAL Championship
    // slate directly (SlateNumber 18) and add IND@ATL as a second game in it, using the admin user
    // (frizat) for the replay test — admin has zero pre-existing CFB picks in any slate, unlike
    // every demo user, so its "Pick" button isn't already exhausted by a real seeded pick.
    private const int ReplayCfbSeason = 2025;
    private const int ReplayCfbSlateNumber = 18;

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

    // The only leagues the seeder itself ever creates — anything else in LeagueInfo is leftover
    // test data (e.g. self-serve/admin "Create League" during manual testing) that should never
    // survive a reseed.
    private static readonly string[] CanonicalLeagueNames = ["Demo League", "CFB Demo League"];

    public async Task SeedAsync()
    {
        Log.Information("DemoDataSeeder: starting seed for season {Season} week {Week}", DemoSeason, DemoWeek);

        await PurgeUnknownLeaguesAsync();
        await SeedNflWeekAsync();
        await SeedSpreadsAsync();
        var league = await SeedLeagueAsync();
        await SeedLeagueMemberAsync(league);
        await SeedLeagueJuiceMappingAsync(league);
        await SeedNflScoresAsync();
        await SeedDemoUsersAsync(league);
        await SeedHistoricalWeeksAsync(league);

        if (configuration["DEMO_REPLAY_MODE"] == "true")
            await SeedReplayGameSpreadAsync();

        // CFB demo data
        var cfbLeague = await SeedCfbLeagueAsync();
        await SeedCfbLeagueMembersAsync(cfbLeague);
        await SeedLeagueJuiceMappingAsync(cfbLeague);
        await SeedCfbWeekConfigAsync();
        var slates = await SeedCfbSlatesAsync();
        await SeedCfbSpreadsAsync(slates);
        await SeedCfbScoresAsync(slates);
        await SeedCfbPicksAsync(cfbLeague, slates);

        if (configuration["DEMO_REPLAY_MODE"] == "true")
            await SeedReplayCfbSlateAsync();

        Log.Information("DemoDataSeeder: seed complete");
    }

    /// <summary>
    /// Deletes any LeagueInfo row (and its LeagueUserMapping/LeagueJuiceMapping/NflPicks/CfbPicks/
    /// Invitations) that the seeder didn't itself create. Self-serve/admin "Create League" testing
    /// against the local demo stack otherwise leaves permanent debris behind, since the rest of
    /// SeedAsync only ever find-or-creates the two canonical leagues by name — it never touches,
    /// let alone clears, anything else in LeagueInfo.
    /// </summary>
    public async Task PurgeUnknownLeaguesAsync()
    {
        var strayLeagueIds = await db.LeagueInfo
            .Where(l => !CanonicalLeagueNames.Contains(l.LeagueName))
            .Select(l => l.Id)
            .ToListAsync();

        if (strayLeagueIds.Count == 0) return;

        LeagueCascadeDelete.RemoveLeaguesAndDependents(db, strayLeagueIds);
        await db.SaveChangesAsync();

        Log.Information("DemoDataSeeder: purged {Count} stray league(s) not created by the seeder", strayLeagueIds.Count);
    }

    // frizat: this class's own doc comment promises "Idempotent — safe to call on every startup."
    // Before soft-delete, a removed member's row was hard-deleted, so a bare AnyAsync-then-Add
    // self-healed on reseed. Now the row persists with IsActive=false, so every "ensure this demo
    // member exists" call site must reactivate an existing inactive row (not just skip-if-any-row)
    // to keep that promise once someone exercises the Remove Member feature against the demo
    // stack — mirrors LeagueRepository.AddLeagueUserMappingAsync's reactivate-not-duplicate logic.
    public async Task EnsureActiveLeagueMemberAsync(int leagueId, string userId)
    {
        var existing = await db.LeagueUserMapping
            .FirstOrDefaultAsync(m => m.LeagueId == leagueId && m.UserId == userId);
        if (existing is null) {
            db.LeagueUserMapping.Add(new LeagueUserMapping { LeagueId = leagueId, UserId = userId });
            await db.SaveChangesAsync();
        } else if (!existing.IsActive) {
            existing.IsActive = true;
            existing.RemovedAt = null;
            await db.SaveChangesAsync();
        }
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

    // frizat-703.6: seeds one spread for the exact real game ReplayCacheService replays
    // (IND @ ATL, event 401772636 — see sample_espn_nfl_*.json), into the SAME week the replay
    // fixtures embed (ReplaySeason/ReplayWeek). Only runs when DEMO_REPLAY_MODE=true so normal
    // demo e2e's game-count/team assertions for the frozen Super Bowl week are unaffected.
    private async Task SeedReplayGameSpreadAsync()
    {
        // AddPicks (LeagueController) rejects any pick whose (Season, NflWeek) has no NflWeeks row.
        if (!await db.NflWeeks.AnyAsync(w => w.Season == ReplaySeason && w.NflWeek == ReplayWeek))
        {
            db.NflWeeks.Add(new NflWeeks {
                Season = ReplaySeason,
                NflWeek = ReplayWeek,
                StartDate = DateTimeOffset.UtcNow,
                EndDate = DateTimeOffset.UtcNow.AddDays(7),
            });
            await db.SaveChangesAsync();
        }

        // Self-healing, not skip-if-exists: a long-lived local backend process can outlive the
        // "future" GameTime seeded on a previous startup, which would silently make the replay
        // game un-pickable without ever touching the DB by hand. Re-seeding fresh on every
        // restart (idempotent — same row, updated in place) is what CI does naturally anyway.
        var existing = await db.NflSpreads.FirstOrDefaultAsync(s =>
            s.Season == ReplaySeason && s.NflWeek == ReplayWeek &&
            s.HomeTeam == "IND" && s.AwayTeam == "ATL");
        if (existing is not null)
        {
            existing.GameTime = DateTimeOffset.UtcNow.AddHours(2);
            await db.SaveChangesAsync();
            Log.Information("DemoDataSeeder: refreshed replay game spread GameTime for {Season}/{Week}", ReplaySeason, ReplayWeek);
            return;
        }

        db.NflSpreads.Add(new NflSpreads {
            Season = ReplaySeason,
            NflWeek = ReplayWeek,
            HomeTeam = "IND",
            AwayTeam = "ATL",
            HomeTeamSpread = -3.5,
            AwayTeamSpread = 3.5,
            OverUnder = 47.5,
            GameTime = DateTimeOffset.UtcNow.AddHours(2),
        });
        await db.SaveChangesAsync();
        Log.Information("DemoDataSeeder: seeded replay game spread IND@ATL for {Season}/{Week}", ReplaySeason, ReplayWeek);
    }

    // frizat-703.6: CFB side of the replay game — same underlying ReplayCacheService snapshots
    // (IND @ ATL), added as a second game in the REAL Championship slate so it's surfaced through
    // CFB's normal slate-based picks/scores flow (proving the SSE push path — /api/cfb/live-stream
    // — separately from NFL's poll path) without inventing a slate number the frontend doesn't
    // recognize. CfbSpreads.GameTime (not the ESPN payload's date) drives CFB's pick-lock check,
    // so it's set directly here. Requires SeedHistoricalWeeksAsync-equivalent CFB seeding (the
    // Championship slate) to have already run.
    private async Task SeedReplayCfbSlateAsync()
    {
        var slate = await db.CfbSlates.FirstOrDefaultAsync(s =>
            s.Season == ReplayCfbSeason && s.SlateNumber == ReplayCfbSlateNumber);
        if (slate is null)
        {
            Log.Warning("DemoDataSeeder: CFB Championship slate {Season}/{Slate} not found — skipping replay CFB seed", ReplayCfbSeason, ReplayCfbSlateNumber);
            return;
        }

        // Self-healing (see SeedReplayGameSpreadAsync) — CFB's pick-lock check reads
        // CfbSpreads.GameTime directly (not a re-derived value), so it's especially prone to
        // going stale across a long-lived local backend process.
        var existing = await db.CfbSpreads.FirstOrDefaultAsync(s =>
            s.CfbSlateId == slate.Id && s.HomeTeam == "IND" && s.AwayTeam == "ATL");
        if (existing is not null)
        {
            existing.GameTime = DateTimeOffset.UtcNow.AddMinutes(30);
            await db.SaveChangesAsync();
            Log.Information("DemoDataSeeder: refreshed replay CFB spread GameTime for slate {Slate}", ReplayCfbSlateNumber);
            return;
        }

        db.CfbSpreads.Add(new CfbSpreads {
            CfbSlateId = slate.Id,
            HomeTeam = "IND",
            AwayTeam = "ATL",
            HomeTeamSpread = -3.5,
            AwayTeamSpread = 3.5,
            OverUnder = 47.5,
            GameTime = DateTimeOffset.UtcNow.AddMinutes(30),
            IsLeagueEligible = true,
        });
        await db.SaveChangesAsync();
        Log.Information("DemoDataSeeder: seeded replay CFB slate {Slate} spread IND@ATL", ReplayCfbSlateNumber);
    }

    private async Task FixSpreadAbbreviationsAsync()
    {
        // Correct legacy abbreviations that were seeded before the ESPN mapping was applied
        var fixes = new Dictionary<string, string> { ["WSH"] = "WAS", ["ARZ"] = "ARI" };
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

        await EnsureActiveLeagueMemberAsync(league.Id, adminUser.Id);
        Log.Information("DemoDataSeeder: added admin to Demo League");
    }

    private async Task SeedNflScoresAsync()
    {
        if (await db.NflScores.AnyAsync(s => s.Season == DemoSeason && s.NflWeek == DemoWeek))
            return;

        // All 16 final games for 2025 week 18, matching every game in DemoGames/SeedSpreadsAsync —
        // GetWeekScoresAsync (DemoEspnCacheService) now synthesizes the Scores-page response from
        // this table, so every team referenced in DemoPicksMap (e.g. Alice's BUF pick) needs a
        // matching final game here, not just a hand-picked subset. Originally only 4 games (the
        // ones frozen in sample_espn_nfl.json) were seeded here, back when GetWeekScores still
        // fell through to live ESPN for the rest — that gap was invisible until the DB became the
        // sole source of truth for this week.
        var scores = new List<NflScores>
        {
            new() { Season = DemoSeason, NflWeek = DemoWeek, HomeTeam = "BUF", AwayTeam = "TB",  HomeTeamScore = 27, AwayTeamScore = 20, GameTime = new DateTimeOffset(2026, 1, 4, 18, 0, 0, TimeSpan.Zero) },
            new() { Season = DemoSeason, NflWeek = DemoWeek, HomeTeam = "DAL", AwayTeam = "LAR", HomeTeamScore = 28, AwayTeamScore = 20, GameTime = new DateTimeOffset(2026, 1, 4, 18, 0, 0, TimeSpan.Zero) },
            new() { Season = DemoSeason, NflWeek = DemoWeek, HomeTeam = "GB",  AwayTeam = "MIN", HomeTeamScore = 17, AwayTeamScore = 24, GameTime = new DateTimeOffset(2026, 1, 4, 18, 0, 0, TimeSpan.Zero) },
            new() { Season = DemoSeason, NflWeek = DemoWeek, HomeTeam = "TEN", AwayTeam = "ATL", HomeTeamScore = 20, AwayTeamScore = 24, GameTime = new DateTimeOffset(2026, 1, 4, 18, 0, 0, TimeSpan.Zero) },
            new() { Season = DemoSeason, NflWeek = DemoWeek, HomeTeam = "IND", AwayTeam = "NO",  HomeTeamScore = 27, AwayTeamScore = 17, GameTime = new DateTimeOffset(2026, 1, 4, 18, 0, 0, TimeSpan.Zero) },
            new() { Season = DemoSeason, NflWeek = DemoWeek, HomeTeam = "MIA", AwayTeam = "NE",  HomeTeamScore = 31, AwayTeamScore = 17, GameTime = new DateTimeOffset(2026, 1, 4, 18, 0, 0, TimeSpan.Zero) },
            new() { Season = DemoSeason, NflWeek = DemoWeek, HomeTeam = "NYG", AwayTeam = "NYJ", HomeTeamScore = 16, AwayTeamScore = 20, GameTime = new DateTimeOffset(2026, 1, 4, 18, 0, 0, TimeSpan.Zero) },
            new() { Season = DemoSeason, NflWeek = DemoWeek, HomeTeam = "PIT", AwayTeam = "JAC", HomeTeamScore = 23, AwayTeamScore = 20, GameTime = new DateTimeOffset(2026, 1, 4, 18, 0, 0, TimeSpan.Zero) },
            new() { Season = DemoSeason, NflWeek = DemoWeek, HomeTeam = "WAS", AwayTeam = "PHI", HomeTeamScore = 7,  AwayTeamScore = 38, GameTime = new DateTimeOffset(2026, 1, 4, 18, 0, 0, TimeSpan.Zero) },
            new() { Season = DemoSeason, NflWeek = DemoWeek, HomeTeam = "CAR", AwayTeam = "HOU", HomeTeamScore = 24, AwayTeamScore = 27, GameTime = new DateTimeOffset(2026, 1, 4, 18, 0, 0, TimeSpan.Zero) },
            new() { Season = DemoSeason, NflWeek = DemoWeek, HomeTeam = "SEA", AwayTeam = "CLE", HomeTeamScore = 27, AwayTeamScore = 13, GameTime = new DateTimeOffset(2026, 1, 4, 21, 25, 0, TimeSpan.Zero) },
            new() { Season = DemoSeason, NflWeek = DemoWeek, HomeTeam = "DEN", AwayTeam = "KC",  HomeTeamScore = 24, AwayTeamScore = 27, GameTime = new DateTimeOffset(2026, 1, 4, 21, 25, 0, TimeSpan.Zero) },
            new() { Season = DemoSeason, NflWeek = DemoWeek, HomeTeam = "ARI", AwayTeam = "BAL", HomeTeamScore = 17, AwayTeamScore = 31, GameTime = new DateTimeOffset(2026, 1, 4, 21, 25, 0, TimeSpan.Zero) },
            new() { Season = DemoSeason, NflWeek = DemoWeek, HomeTeam = "SF",  AwayTeam = "CIN", HomeTeamScore = 24, AwayTeamScore = 20, GameTime = new DateTimeOffset(2026, 1, 4, 21, 25, 0, TimeSpan.Zero) },
            new() { Season = DemoSeason, NflWeek = DemoWeek, HomeTeam = "LAC", AwayTeam = "CHI", HomeTeamScore = 21, AwayTeamScore = 27, GameTime = new DateTimeOffset(2026, 1, 5, 1, 20, 0, TimeSpan.Zero) },
            new() { Season = DemoSeason, NflWeek = DemoWeek, HomeTeam = "DET", AwayTeam = "LV",  HomeTeamScore = 31, AwayTeamScore = 17, GameTime = new DateTimeOffset(2026, 1, 5, 1, 15, 0, TimeSpan.Zero) },
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

        // Ensure league membership
        await EnsureActiveLeagueMemberAsync(league.Id, user.Id);

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

    // Historical weeks 1-17: same 16 games every week (all 32 NFL teams), home teams always cover
    // 4 picks per user per week (required picks = 4)
    // Winning user picks: KC, DAL, PHI, BUF (first 4 home teams — all cover)
    // Losing user picks:  DEN, CLE, NYG, NYJ (first 4 away teams — none cover)
    private static readonly string[] HistWinPicks = ["KC", "DAL", "PHI", "BUF"];
    private static readonly string[] HistLosePicks = ["DEN", "CLE", "NYG", "NYJ"];

    // 16 game pairings covering all 32 NFL teams — same matchups every regular season week 1-17
    // Real game data comes from ESPN API; these spreads are for demo purposes only
    private static readonly (string Home, string Away, double HomeSpread, double OU)[] HistGames =
    [
        ("KC",  "DEN", -7.0, 47.5),
        ("DAL", "CLE", -6.0, 44.5),
        ("PHI", "NYG", -4.0, 43.5),
        ("BUF", "NYJ", -3.0, 46.5),
        ("BAL", "PIT", -5.5, 44.0),
        ("HOU", "JAC", -4.5, 43.0),
        ("TEN", "IND",  2.5, 41.5),
        ("MIA", "NE",  -3.5, 45.5),
        ("LAR", "SEA", -2.5, 46.5),
        ("SF",  "ARI", -9.5, 47.0),
        ("LAC", "LV",  -3.5, 44.0),
        ("GB",  "MIN", -2.5, 46.0),
        ("DET", "CHI", -7.0, 46.5),
        ("TB",  "ATL", -3.0, 44.5),
        ("NO",  "CAR",  5.5, 40.5),
        ("WAS", "CIN",  1.5, 43.5),
    ];

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

    // Real 2025 NFL Playoffs — home/away verified against ESPN API responses.
    // Wild Card (NflWeek 19 = ESPN postseason week 1): Jan 11-12 2026
    private static readonly (string Home, string Away, double HomeSpread, double AwaySpread, double OU, int HomeScore, int AwayScore, DateTimeOffset GameTime)[] WildCardGames =
    [
        // ESPN home team listed first — verified from scoreboard API
        ("CAR", "LAR",  6.5, -6.5, 48.5, 31, 34, new DateTimeOffset(2026, 1, 11, 18, 0, 0, TimeSpan.Zero)),  // LAR wins, CAR covers
        ("CHI", "GB",  -3.5,  3.5, 45.5, 31, 27, new DateTimeOffset(2026, 1, 11, 21, 30, 0, TimeSpan.Zero)), // CHI wins and covers
        ("JAC", "BUF",  9.5, -9.5, 47.5, 24, 27, new DateTimeOffset(2026, 1, 11, 21, 30, 0, TimeSpan.Zero)), // BUF wins, JAC covers
        ("PHI", "SF",   3.5, -3.5, 48.5, 19, 23, new DateTimeOffset(2026, 1, 12, 18, 0, 0, TimeSpan.Zero)),  // SF wins, PHI doesn't cover
        ("NE",  "LAC", -5.5,  5.5, 44.5, 16,  3, new DateTimeOffset(2026, 1, 12, 21, 30, 0, TimeSpan.Zero)), // NE wins and covers
        ("PIT", "HOU",  6.5, -6.5, 43.5,  6, 30, new DateTimeOffset(2026, 1, 12, 21, 30, 0, TimeSpan.Zero)), // HOU wins, PIT doesn't cover
    ];

    // Divisional (NflWeek 20 = ESPN postseason week 2): Jan 18-19 2026
    private static readonly (string Home, string Away, double HomeSpread, double AwaySpread, double OU, int HomeScore, int AwayScore, DateTimeOffset GameTime)[] DivisionalGames =
    [
        ("DEN", "BUF", -2.5,  2.5, 49.5, 33, 30, new DateTimeOffset(2026, 1, 18, 18, 0, 0, TimeSpan.Zero)),  // DEN wins
        ("SEA", "SF",  -3.0,  3.0, 45.0, 41,  6, new DateTimeOffset(2026, 1, 18, 21, 30, 0, TimeSpan.Zero)), // SEA wins
        ("NE",  "HOU", -1.5,  1.5, 44.5, 28, 16, new DateTimeOffset(2026, 1, 19, 18, 0, 0, TimeSpan.Zero)),  // NE wins
        ("CHI", "LAR",  4.5, -4.5, 46.5, 17, 20, new DateTimeOffset(2026, 1, 19, 21, 30, 0, TimeSpan.Zero)), // LAR wins, CHI covers
    ];

    // Conference Championship (NflWeek 21 = ESPN postseason week 3): Jan 26 2026
    private static readonly (string Home, string Away, double HomeSpread, double AwaySpread, double OU, int HomeScore, int AwayScore, DateTimeOffset GameTime)[] ConfChampGames =
    [
        ("DEN", "NE",   3.5, -3.5, 44.5,  7, 10, new DateTimeOffset(2026, 1, 26, 18, 0, 0, TimeSpan.Zero)),  // NE wins, DEN covers
        ("SEA", "LAR", -4.5,  4.5, 46.5, 31, 27, new DateTimeOffset(2026, 1, 26, 21, 30, 0, TimeSpan.Zero)), // SEA wins, doesn't cover
    ];

    // Super Bowl (NflWeek 22 = ESPN postseason week 4 via NflScoresJob wk5→4 hack): Feb 9 2026 — NE home (ESPN convention), SEA wins
    private static readonly (string Home, string Away, double HomeSpread, double AwaySpread, double OU, int HomeScore, int AwayScore, DateTimeOffset GameTime)[] SuperBowlGames =
    [
        ("NE",  "SEA", -2.5,  2.5, 45.5, 13, 29, new DateTimeOffset(2026, 2, 9, 23, 30, 0, TimeSpan.Zero)),  // SEA wins
    ];

    // Postseason picks per user (true = home team, false = away team)
    // Wild Card has 6 games but only 3 picks required (same as NFL GetRequiredPicks(19)=3)
    // Wild Card: CAR/LAR, CHI/GB, JAC/BUF, PHI/SF, NE/LAC, PIT/HOU — picking first 3 games
    // Results:  LAR wins, CHI wins, BUF wins (first 3 game results)
    private static readonly Dictionary<string, bool[]> WildCardPicks = new()
    {
        ["Alice"]  = [false, true,  false],  // LAR, CHI, BUF (3 winners)
        ["Bob"]    = [true,  false, true],   // CAR, GB, JAC (3 losers)
        ["Carlos"] = [false, true,  false],  // LAR, CHI, BUF
        ["Dana"]   = [true,  false, true],   // CAR, GB, JAC
        ["Eve"]    = [false, true,  true],   // LAR, CHI, JAC (2 winners, 1 loser)
    };

    // Divisional has 4 games and 3 picks required (GetRequiredPicks(20)=3)
    // Divisional: DEN/BUF, SEA/SF, NE/HOU, CHI/LAR — picking first 3 games
    // Results: DEN covers (+3.5, wins 33-30), SEA covers (-3.0, wins 41-6), NE covers (-1.5, wins 28-16)
    private static readonly Dictionary<string, bool[]> DivisionalPicks = new()
    {
        ["Alice"]  = [true,  true,  true],   // DEN, SEA, NE (all cover — wins)
        ["Bob"]    = [false, false, false],  // BUF, SF, HOU (none cover — loses)
        ["Carlos"] = [true,  true,  true],   // DEN, SEA, NE
        ["Dana"]   = [false, false, false],  // BUF, SF, HOU
        ["Eve"]    = [true,  false, true],   // DEN, SF, NE (SF loses — Eve loses week)
    };

    // Conference Championship has 2 games and 2 picks required (GetRequiredPicks(21)=2)
    // Conf. Champ: DEN/NE, SEA/LAR
    // Results: DEN covers (+3.5, loses 7-10 but covers), LAR covers (+4.5, loses 27-31 but covers)
    private static readonly Dictionary<string, bool[]> ConfChampPicks = new()
    {
        ["Alice"]  = [false, true],   // NE (doesn't cover), SEA (doesn't cover) — Alice loses
        ["Bob"]    = [true,  false],  // DEN (covers), LAR (covers) — Bob wins
        ["Carlos"] = [false, true],   // NE, SEA — Carlos loses
        ["Dana"]   = [true,  false],  // DEN, LAR — Dana wins
        ["Eve"]    = [false, false],  // NE (loses), LAR (wins) — Eve loses (not all correct)
    };

    // Super Bowl: NE home, SEA away. SEA wins. true=NE(home), false=SEA(away)
    private static readonly Dictionary<string, bool> SuperBowlPicksMap = new()
    {
        ["Alice"]  = false,  // SEA (wins)
        ["Bob"]    = true,   // NE
        ["Carlos"] = false,  // SEA (wins)
        ["Dana"]   = true,   // NE
        ["Eve"]    = false,  // SEA (wins)
    };

    private async Task SeedHistoricalWeeksAsync(LeagueInfo? league)
    {
        if (league == null) return;
        if (await db.NflPicks.AnyAsync(p => p.Season == DemoSeason && p.NflWeek == 1))
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

            // NflSpreads (16 games, all 32 NFL teams, home teams favored)
            foreach (var g in HistGames)
                db.NflSpreads.Add(Spread(week, g.Home, g.Away, g.HomeSpread, -g.HomeSpread, g.OU, weekGameTime.ToString("o")));

            // NflScores (all home teams win and cover — home score exceeds away score + spread margin)
            var histScores = new[]
            {
                ("KC",  "DEN", 24, 14), ("DAL", "CLE", 28, 20), ("PHI", "NYG", 20, 13), ("BUF", "NYJ", 17, 10),
                ("BAL", "PIT", 21, 13), ("HOU", "JAC", 27, 20), ("TEN", "IND", 24, 17), ("MIA", "NE",  20, 13),
                ("LAR", "SEA", 24, 20), ("SF",  "ARI", 31, 17), ("LAC", "LV",  20, 14), ("GB",  "MIN", 24, 20),
                ("DET", "CHI", 28, 17), ("TB",  "ATL", 20, 14), ("NO",  "CAR", 27, 17), ("WAS", "CIN", 17, 14),
            };
            foreach (var (home, away, hs, as_) in histScores)
                db.NflScores.Add(new NflScores { Season = DemoSeason, NflWeek = week, HomeTeam = home, AwayTeam = away, HomeTeamScore = hs, AwayTeamScore = as_, GameTime = weekGameTime });
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
        // NflScoresJob maps ESPN wk5→4 via (j==5?4:j), so GetWeekFromEspnWeek(4,true)=22
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

            // Only seed as many picks as the pattern specifies — pattern.Length = required picks
            // for this round. frizat: Over/Under is an alternate pick TYPE, not an additional
            // pick beyond the week's required count — AddPicks' server-side validation caps total
            // picks (any type) at GetRequiredPicks(week), so a real user could never submit
            // pattern.Length spread picks PLUS a separate O/U pick (confirmed via direct DB
            // query: Bob had 4 rows for NflWeek 19, a 3-required-pick week). Bob/Dana's O/U pick
            // now replaces their game-0 spread pick instead of adding to it.
            for (int i = 0; i < Math.Min(pattern.Length, games.Length); i++)
            {
                if (i == 0 && name == "Bob") {
                    db.NflPicks.Add(new NflPicks { UserId = user.Id, LeagueId = league.Id, Team = games[0].Home, Pick = PickType.Over, NflWeek = week, Season = DemoSeason, NflWeekId = nflWeek.Id, DateCreated = DateTimeOffset.UtcNow });
                    continue;
                }
                if (i == 0 && name == "Dana") {
                    db.NflPicks.Add(new NflPicks { UserId = user.Id, LeagueId = league.Id, Team = games[0].Home, Pick = PickType.Under, NflWeek = week, Season = DemoSeason, NflWeekId = nflWeek.Id, DateCreated = DateTimeOffset.UtcNow });
                    continue;
                }
                var team = pattern[i] ? games[i].Home : games[i].Away;
                db.NflPicks.Add(new NflPicks
                {
                    UserId = user.Id, LeagueId = league.Id, Team = team,
                    Pick = PickType.Spread, NflWeek = week, Season = DemoSeason,
                    NflWeekId = nflWeek.Id, DateCreated = DateTimeOffset.UtcNow,
                });
            }
        }
        await db.SaveChangesAsync();
        Log.Information("DemoDataSeeder: seeded postseason week {Week} ({Label}) with O/U picks", week, label);
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

    // Conference Championships (slate 14 in new 18-slate system)
    private static readonly (int SlateIdx, int EventId, string Home, string Away, double HomeSpread, double AwaySpread, double OU, int HomeScore, int AwayScore, DateTimeOffset GameTime)[] Slate15Games =
    [
        (14, 401700201, "OSU",  "IU",   -6.5,  6.5, 56.5, 34, 31, new DateTimeOffset(2025, 12,  6, 17,  0, 0, TimeSpan.Zero)),
        (14, 401700202, "ALA",  "UGA",  -3.5,  3.5, 54.5, 24, 17, new DateTimeOffset(2025, 12,  6, 20,  0, 0, TimeSpan.Zero)),
        (14, 401700203, "ND",   "CLEM", -5.5,  5.5, 51.5, 28, 21, new DateTimeOffset(2025, 12,  6, 22, 30, 0, TimeSpan.Zero)),
        (14, 401700204, "ORE",  "BOIS", -9.5,  9.5, 54.0, 35, 21, new DateTimeOffset(2025, 12,  6, 20,  0, 0, TimeSpan.Zero)),
        (14, 401700205, "KSU",  "OU",   -3.0,  3.0, 51.0, 24, 17, new DateTimeOffset(2025, 12,  6, 16,  0, 0, TimeSpan.Zero)),
        (14, 401700206, "MISS", "TAMU", -2.5,  2.5, 56.5, 28, 24, new DateTimeOffset(2025, 12,  7, 17,  0, 0, TimeSpan.Zero)),
    ];

    // 2025 CFP matchups (real bracket, all final as of Jan 2026)
    // SlateIdx now refers to SlateNumber (15=First Round, 16=QF, 17=SF, 18=Championship)
    private static readonly (int SlateIdx, int EventId, string Home, string Away, double HomeSpread, double AwaySpread, double OU, int HomeScore, int AwayScore, DateTimeOffset GameTime)[] CfpGames =
    [
        // Slate 15: First Round (Dec 19-20)
        (15, 401800001, "ORE",  "JMU",  -24.5, 24.5, 52.5, 38, 10, new DateTimeOffset(2025, 12, 19, 20,  0, 0, TimeSpan.Zero)),
        (15, 401800002, "MISS", "TULN", -17.5, 17.5, 58.0, 35, 17, new DateTimeOffset(2025, 12, 19, 23, 30, 0, TimeSpan.Zero)),
        (15, 401800003, "TAMU", "MIA",   -7.0,  7.0, 49.5, 24, 17, new DateTimeOffset(2025, 12, 20, 20,  0, 0, TimeSpan.Zero)),
        (15, 401800004, "OU",   "ALA",   -3.0,  3.0, 51.0, 21, 14, new DateTimeOffset(2025, 12, 20, 23, 30, 0, TimeSpan.Zero)),
        // Slate 16: Quarterfinals (Dec 31/Jan 1)
        (16, 401800005, "IU",   "ALA",   -3.5,  3.5, 48.5, 27, 24, new DateTimeOffset(2026,  1,  1, 17,  0, 0, TimeSpan.Zero)),
        (16, 401800006, "UGA",  "MISS", -10.0, 10.0, 55.0, 35, 21, new DateTimeOffset(2026,  1,  1, 20, 30, 0, TimeSpan.Zero)),
        (16, 401800007, "ORE",  "TTU",   -3.5,  3.5, 53.0, 31, 20, new DateTimeOffset(2025, 12, 31, 20,  0, 0, TimeSpan.Zero)),
        (16, 401800008, "MIA",  "OSU",    7.0, -7.0, 56.5, 28, 24, new DateTimeOffset(2025, 12, 31, 23, 30, 0, TimeSpan.Zero)),
        // Slate 17: Semifinals (Jan 8-9)
        (17, 401800009, "IU",   "ORE",   -3.0,  3.0, 51.5, 34, 27, new DateTimeOffset(2026,  1,  9, 20,  0, 0, TimeSpan.Zero)),
        (17, 401800010, "MIA",  "UGA",    3.5, -3.5, 50.0, 21, 17, new DateTimeOffset(2026,  1,  8, 20,  0, 0, TimeSpan.Zero)),
        // Slate 18: Championship (Jan 19) — IN PROGRESS Q3: IU 14, MIA 7 (IU winning)
        (18, 401800011, "IU",   "MIA",   -3.0,  3.0, 46.5, 14,  7, new DateTimeOffset(2026,  1, 19, 23, 30, 0, TimeSpan.Zero)),
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
        if (adminUser != null)
            await EnsureActiveLeagueMemberAsync(league.Id, adminUser.Id);

        foreach (var (_, email) in DemoUsers)
        {
            var user = await userManager.FindByEmailAsync(email);
            if (user == null) continue;
            await EnsureActiveLeagueMemberAsync(league.Id, user.Id);
        }
        Log.Information("DemoDataSeeder: added all demo users to CFB Demo League");
    }

    private const int CfbExpectedSlateCount = 18;

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
            // Regular season weeks 1-13
            new() { Season = CfbDemoSeason, SlateNumber =  1, Label = "Week 1",  SlateType = "RegularSeason",         EspnWeekNumber =  1, ScoringFormat = "Standard",      StartDate = new DateOnly(2025,  8, 23), EndDate = new DateOnly(2025,  8, 30) },
            new() { Season = CfbDemoSeason, SlateNumber =  2, Label = "Week 2",  SlateType = "RegularSeason",         EspnWeekNumber =  2, ScoringFormat = "Standard",      StartDate = new DateOnly(2025,  8, 30), EndDate = new DateOnly(2025,  9,  6) },
            new() { Season = CfbDemoSeason, SlateNumber =  3, Label = "Week 3",  SlateType = "RegularSeason",         EspnWeekNumber =  3, ScoringFormat = "Standard",      StartDate = new DateOnly(2025,  9,  6), EndDate = new DateOnly(2025,  9, 13) },
            new() { Season = CfbDemoSeason, SlateNumber =  4, Label = "Week 4",  SlateType = "RegularSeason",         EspnWeekNumber =  4, ScoringFormat = "Standard",      StartDate = new DateOnly(2025,  9, 13), EndDate = new DateOnly(2025,  9, 20) },
            new() { Season = CfbDemoSeason, SlateNumber =  5, Label = "Week 5",  SlateType = "RegularSeason",         EspnWeekNumber =  5, ScoringFormat = "Standard",      StartDate = new DateOnly(2025,  9, 20), EndDate = new DateOnly(2025,  9, 27) },
            new() { Season = CfbDemoSeason, SlateNumber =  6, Label = "Week 6",  SlateType = "RegularSeason",         EspnWeekNumber =  6, ScoringFormat = "Standard",      StartDate = new DateOnly(2025,  9, 27), EndDate = new DateOnly(2025, 10,  4) },
            new() { Season = CfbDemoSeason, SlateNumber =  7, Label = "Week 7",  SlateType = "RegularSeason",         EspnWeekNumber =  7, ScoringFormat = "Standard",      StartDate = new DateOnly(2025, 10,  4), EndDate = new DateOnly(2025, 10, 11) },
            new() { Season = CfbDemoSeason, SlateNumber =  8, Label = "Week 8",  SlateType = "RegularSeason",         EspnWeekNumber =  8, ScoringFormat = "Standard",      StartDate = new DateOnly(2025, 10, 11), EndDate = new DateOnly(2025, 10, 18) },
            new() { Season = CfbDemoSeason, SlateNumber =  9, Label = "Week 9",  SlateType = "RegularSeason",         EspnWeekNumber =  9, ScoringFormat = "Standard",      StartDate = new DateOnly(2025, 10, 18), EndDate = new DateOnly(2025, 10, 25) },
            new() { Season = CfbDemoSeason, SlateNumber = 10, Label = "Week 10", SlateType = "RegularSeason",         EspnWeekNumber = 10, ScoringFormat = "Standard",      StartDate = new DateOnly(2025, 10, 25), EndDate = new DateOnly(2025, 11,  1) },
            new() { Season = CfbDemoSeason, SlateNumber = 11, Label = "Week 11", SlateType = "RegularSeason",         EspnWeekNumber = 11, ScoringFormat = "Standard",      StartDate = new DateOnly(2025, 11,  1), EndDate = new DateOnly(2025, 11,  8) },
            new() { Season = CfbDemoSeason, SlateNumber = 12, Label = "Week 12", SlateType = "RegularSeason",         EspnWeekNumber = 12, ScoringFormat = "Standard",      StartDate = new DateOnly(2025, 11,  8), EndDate = new DateOnly(2025, 11, 15) },
            new() { Season = CfbDemoSeason, SlateNumber = 13, Label = "Week 13", SlateType = "RegularSeason",         EspnWeekNumber = 13, ScoringFormat = "Standard",      StartDate = new DateOnly(2025, 11, 15), EndDate = new DateOnly(2025, 11, 22) },
            // Conference Championship Week (slate 14 — 4 picks, same as regular season)
            new() { Season = CfbDemoSeason, SlateNumber = 14, Label = "Conf. Championships", SlateType = "ConferenceChampionship", EspnWeekNumber = 14, ScoringFormat = "Standard",      StartDate = new DateOnly(2025, 12,  5), EndDate = new DateOnly(2025, 12,  7) },
            // CFP Postseason
            new() { Season = CfbDemoSeason, SlateNumber = 15, Label = "CFP First Round",   SlateType = "FirstRound",   EspnWeekNumber = 16, ScoringFormat = "NFLDivisional", StartDate = new DateOnly(2025, 12, 19), EndDate = new DateOnly(2025, 12, 20) },
            new() { Season = CfbDemoSeason, SlateNumber = 16, Label = "CFP Quarterfinals", SlateType = "Quarterfinal", EspnWeekNumber = 18, ScoringFormat = "NFLDivisional", StartDate = new DateOnly(2025, 12, 31), EndDate = new DateOnly(2026,  1,  1) },
            new() { Season = CfbDemoSeason, SlateNumber = 17, Label = "CFP Semifinals",    SlateType = "Semifinal",    EspnWeekNumber = 20, ScoringFormat = "NFLConference", StartDate = new DateOnly(2026,  1,  8), EndDate = new DateOnly(2026,  1,  9) },
            new() { Season = CfbDemoSeason, SlateNumber = 18, Label = "CFP Championship",  SlateType = "Championship", EspnWeekNumber = 21, ScoringFormat = "NFLSuperBowl",  StartDate = new DateOnly(2026,  1, 19), EndDate = new DateOnly(2026,  1, 19) },
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
            .Concat(Slate15Games)
            .Concat(CfpGames);

        var spreads = allGames.Select(g => new CfbSpreads
        {
            CfbSlateId     = slates.First(s => s.SlateNumber == g.SlateIdx).Id,
            HomeTeam       = g.Home,
            AwayTeam       = g.Away,
            HomeTeamSpread = g.HomeSpread,
            AwayTeamSpread = g.AwaySpread,
            OverUnder      = g.OU,
            GameTime       = g.GameTime,
            IsLeagueEligible = true,
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
            .Concat(Slate15Games)
            .Concat(CfpGames);

        var scores = allGames.Select(g => new CfbScores
        {
            CfbSlateId    = slates.First(s => s.SlateNumber == g.SlateIdx).Id,
            HomeTeam      = g.Home,
            AwayTeam      = g.Away,
            HomeTeamScore = g.HomeScore,
            AwayTeamScore = g.AwayScore,
            // Championship (slate 18) is in-progress so we can show field position in demo
            GameStatus    = g.SlateIdx == 18 ? "StatusInProgress" : "StatusFinal",
            GameTime      = g.GameTime,
        }).ToList();

        db.CfbScores.AddRange(scores);
        await db.SaveChangesAsync();
        Log.Information("DemoDataSeeder: seeded {Count} CFB scores (Championship in-progress, all others final)", scores.Count);
    }

    // CFB pick patterns — true = home team, false = away team
    // Alice: always home (favorites)
    // Bob: always away (underdogs)
    // Carlos: home for games 1,3,5 and away for 2,4,6
    // Dana: away for games 1,3,5 and home for 2,4,6
    // Eve: home for games 1,2,4 and away for 3,5,6
    // Regular season: 4 picks required per slate (slate has 6 games; pick first 4)
    // frizat: admin (the seeded ADMIN_USERNAME account) mirrors Alice's picks in every CFB
    // pattern below — admin previously had zero CFB picks seeded at all (only NFL), unlike every
    // other demo user, which left "My Picks" empty for admin on the CFB site.
    private static readonly Dictionary<string, bool[]> CfbRegularSeasonPickPattern = new()
    {
        ["Alice"]  = [true,  true,  true,  true],
        ["frizat"] = [true,  true,  true,  true],
        ["Bob"]    = [false, false, false, false],
        ["Carlos"] = [true,  false, true,  false],
        ["Dana"]   = [false, true,  false, true],
        ["Eve"]    = [true,  true,  false, true],
    };

    // Conf. Championships (slate 14): 4 picks from 6 games (same as regular season)
    // Slate 14 games: OSU/IU, ALA/UGA, ND/CLEM, ORE/BOIS, KSU/OU, MISS/TAMU (picking first 4)
    private static readonly Dictionary<string, bool[]> CfbConfChampPicks = new()
    {
        ["Alice"]  = [true,  false, false, true],   // OSU, UGA, CLEM, ORE
        ["frizat"] = [true,  false, false, true],   // OSU, UGA, CLEM, ORE
        ["Bob"]    = [false, true,  true,  false],  // IU, ALA, ND, BOIS
        ["Carlos"] = [true,  false, true,  false],  // OSU, ALA, ND, BOIS
        ["Dana"]   = [false, true,  false, true],   // IU, UGA, CLEM, ORE
        ["Eve"]    = [true,  true,  false, true],   // OSU, UGA, CLEM, ORE
    };

    // CFP First Round (slate 15): 3 picks from 4 games
    // Games: ORE/JMU, MISS/TULN, TAMU/MIA, OU/ALA (picking first 3)
    private static readonly Dictionary<string, bool[]> CfbFirstRoundPicks = new()
    {
        ["Alice"]  = [true,  true,  true],   // ORE, MISS, TAMU
        ["frizat"] = [true,  true,  true],   // ORE, MISS, TAMU
        ["Bob"]    = [false, false, false],  // JMU, TULN, MIA
        ["Carlos"] = [true,  false, true],   // ORE, TULN, TAMU
        ["Dana"]   = [false, true,  false],  // JMU, MISS, MIA
        ["Eve"]    = [true,  true,  true],   // ORE, MISS, TAMU
    };

    // Week 8 games: MICH/PSU, ALA/TENN, OSU/ORE, UGA/MIA, LSU/TAMU, CLEM/FSU — pick first 4
    // CFP QF:       IU/ALA,   UGA/MISS, ORE/TTU,  MIA/OSU
    // CFP SF:       IU/ORE,   MIA/UGA
    // CFP Final:    IU/MIA
    private static readonly Dictionary<string, bool[]> CfbWeek8Picks = new()
    {
        ["Alice"]  = [true,  true,  true,  true],   // MICH, ALA, OSU, UGA
        ["frizat"] = [true,  true,  true,  true],   // MICH, ALA, OSU, UGA
        ["Bob"]    = [false, false, false, false],  // PSU, TENN, ORE, MIA
        ["Carlos"] = [true,  false, true,  false],  // MICH, TENN, OSU, MIA
        ["Dana"]   = [false, true,  false, true],   // PSU, ALA, ORE, UGA
        ["Eve"]    = [true,  true,  false, true],   // MICH, ALA, ORE, UGA
    };

    // CFP Quarterfinals (slate 16): 3 picks from 4 games
    // QF games: IU/ALA, UGA/MISS, ORE/TTU, MIA/OSU — picking first 3
    private static readonly Dictionary<string, bool[]> CfbQfPicks = new()
    {
        ["Alice"]  = [true,  true,  true],   // IU, UGA, ORE
        ["frizat"] = [true,  true,  true],   // IU, UGA, ORE
        ["Bob"]    = [false, false, false],  // ALA, MISS, TTU
        ["Carlos"] = [true,  false, true],   // IU, MISS, ORE
        ["Dana"]   = [false, true,  false],  // ALA, UGA, TTU
        ["Eve"]    = [true,  true,  true],   // IU, UGA, ORE
    };

    // CFP Semifinals (slate 17): 2 picks from 2 games
    // SF games: IU/ORE, MIA/UGA
    private static readonly Dictionary<string, bool[]> CfbSfPicks = new()
    {
        ["Alice"]  = [true,  false],  // IU, UGA (UGA loses — Alice misses 2nd pick)
        ["frizat"] = [true,  false],  // IU, UGA
        ["Bob"]    = [false, true],   // ORE, MIA
        ["Carlos"] = [true,  true],   // IU, MIA
        ["Dana"]   = [false, false],  // ORE, UGA
        ["Eve"]    = [true,  true],   // IU, MIA
    };

    // frizat deliberately has NO entry here: SeedReplayCfbSlateAsync adds a second game (IND@ATL)
    // to this same slate 18, and the CFB replay E2E spec (replay-cfb.spec.ts) runs as admin
    // specifically because admin's single Championship-slate pick slot needs to be free for that
    // replayed game. Giving admin a real pick on the original Championship matchup here would
    // exhaust that slot and break the replay test.
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
        // 6 users × 65 picks each, except admin (frizat) who has no slate-18 pick — see
        // CfbFinalPicks comment (replay E2E needs that slot free):
        // Slates 1-7: 7×4=28, Slate 8: 4, Slates 9-13: 5×4=20, Slate 14 (Conf.Champs): 4
        // Slate 15 (FR): 3, Slate 16 (QF): 3, Slate 17 (SF): 2, Slate 18 (Champ): 1
        // Total per user: 28+4+20+4+3+3+2+1 = 65 → 5×65 + 64 (frizat, no slate-18 pick) = 389
        // frizat: Over/Under is an alternate pick TYPE for Bob/Dana's first pick in each
        // postseason slate (14-18), not an additional pick — CfbPicksController's server-side
        // validation caps total picks (any type) at GetCfbRequiredPicks(slateNumber), so the
        // count above already accounts for it; O/U doesn't add to the total, just recolors 5 of
        // Bob's and 5 of Dana's picks from Spread to Over/Under.
        const int ExpectedPickCount = 389;
        if (await db.CfbPicks.CountAsync(p => p.LeagueId == league.Id) >= ExpectedPickCount) return;
        // Clear any partial seed before re-seeding
        db.CfbPicks.RemoveRange(db.CfbPicks.Where(p => p.LeagueId == league.Id));
        await db.SaveChangesAsync();

        var picks = new List<CfbPicks>();

        void AddPick(int leagueId, string userId, int slateId, string team) =>
            picks.Add(new CfbPicks { UserId = userId, LeagueId = leagueId, CfbSlateId = slateId, Team = team, PickType = "Spread", Season = CfbDemoSeason });

        // frizat: Over/Under is an alternate PICK TYPE for a game, not an additional pick beyond
        // the slate's required count — CfbPicksController's server-side validation enforces total
        // picks (any type) <= GetCfbRequiredPicks(slateNumber), so a real user can never submit
        // more picks than required. This used to call AddPick for every game INCLUDING game 0,
        // then separately add an O/U pick for Bob/Dana on that same game — giving them
        // requiredPicks+1 total picks, which no real submission could ever produce (confirmed via
        // direct DB query: Bob had 4 rows for a 3-required-pick week). Bob/Dana's O/U pick now
        // replaces their game-0 spread pick instead of adding to it.
        void AddFirstPickOrOverUnder(string uName, string userId, int slateId, string homeTeam, string pickedTeam) {
            if (uName == "Bob") {
                picks.Add(new CfbPicks { UserId = userId, LeagueId = league.Id, CfbSlateId = slateId, Team = homeTeam, PickType = "Over", Season = CfbDemoSeason });
                return;
            }
            if (uName == "Dana") {
                picks.Add(new CfbPicks { UserId = userId, LeagueId = league.Id, CfbSlateId = slateId, Team = homeTeam, PickType = "Under", Season = CfbDemoSeason });
                return;
            }
            AddPick(league.Id, userId, slateId, pickedTeam);
        }

        var seedUsernames = DemoUsers.Select(d => d.Username).Append("frizat");
        foreach (var username in seedUsernames)
        {
            var user = await userManager.FindByNameAsync(username);
            if (user == null) continue;

            // Regular season slates 1-7: 4 picks each (pattern.Length = 4, slate has 6 games)
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
                    for (int i = 0; i < Math.Min(rsPattern.Length, gamesArr.Length); i++)
                        AddPick(league.Id, user.Id, slate.Id, rsPattern[i] ? gamesArr[i].Home : gamesArr[i].Away);
                }
            }

            // Week 8: 4 picks from 6 games
            if (CfbWeek8Picks.TryGetValue(username, out var w8) && slates.FirstOrDefault(s => s.SlateNumber == 8) is { } slate8)
                for (int i = 0; i < Math.Min(w8.Length, Week8Games.Length); i++)
                    AddPick(league.Id, user.Id, slate8.Id, w8[i] ? Week8Games[i].Home : Week8Games[i].Away);

            // Regular season slates 9-13: 4 picks each
            if (CfbRegularSeasonPickPattern.TryGetValue(username, out var rsPattern2))
            {
                var regularSlates9to13 = new (int SlateNum, (int SlateIdx, int EventId, string Home, string Away, double HomeSpread, double AwaySpread, double OU, int HomeScore, int AwayScore, DateTimeOffset GameTime)[])[]
                {
                    (9, Slate9Games), (10, Slate10Games), (11, Slate11Games),
                    (12, Slate12Games), (13, Slate13Games),
                };
                foreach (var (slateNum, gamesArr) in regularSlates9to13)
                {
                    if (slates.FirstOrDefault(s => s.SlateNumber == slateNum) is not { } slate) continue;
                    for (int i = 0; i < Math.Min(rsPattern2.Length, gamesArr.Length); i++)
                        AddPick(league.Id, user.Id, slate.Id, rsPattern2[i] ? gamesArr[i].Home : gamesArr[i].Away);
                }
            }

            // Slate 14: Conference Championships — 4 picks total (Bob/Dana's first pick is O/U instead of spread)
            if (CfbConfChampPicks.TryGetValue(username, out var confPattern) && slates.FirstOrDefault(s => s.SlateNumber == 14) is { } slate14Champ)
            {
                for (int i = 0; i < Math.Min(confPattern.Length, Slate15Games.Length); i++) {
                    var team = confPattern[i] ? Slate15Games[i].Home : Slate15Games[i].Away;
                    if (i == 0) AddFirstPickOrOverUnder(username, user.Id, slate14Champ.Id, Slate15Games[0].Home, team);
                    else AddPick(league.Id, user.Id, slate14Champ.Id, team);
                }
            }

            // CFP First Round (slate 15): 3 picks total (Bob/Dana's first pick is O/U instead of spread)
            if (CfbFirstRoundPicks.TryGetValue(username, out var fr15Pattern) && slates.FirstOrDefault(s => s.SlateNumber == 15) is { } slate15)
            {
                var fr15Games = CfpGames.Where(g => g.SlateIdx == 15).ToArray();
                for (int i = 0; i < Math.Min(fr15Pattern.Length, fr15Games.Length); i++) {
                    var team = fr15Pattern[i] ? fr15Games[i].Home : fr15Games[i].Away;
                    if (i == 0) AddFirstPickOrOverUnder(username, user.Id, slate15.Id, fr15Games[0].Home, team);
                    else AddPick(league.Id, user.Id, slate15.Id, team);
                }
            }

            // CFP Quarterfinals (slate 16): 3 picks total (Bob/Dana's first pick is O/U instead of spread)
            if (CfbQfPicks.TryGetValue(username, out var qf) && slates.FirstOrDefault(s => s.SlateNumber == 16) is { } slateQf)
            {
                var qfGames = CfpGames.Where(g => g.SlateIdx == 16).ToArray();
                for (int i = 0; i < Math.Min(qf.Length, qfGames.Length); i++) {
                    var team = qf[i] ? qfGames[i].Home : qfGames[i].Away;
                    if (i == 0) AddFirstPickOrOverUnder(username, user.Id, slateQf.Id, qfGames[0].Home, team);
                    else AddPick(league.Id, user.Id, slateQf.Id, team);
                }
            }

            // CFP Semifinals (slate 17): 2 picks total (Bob/Dana's first pick is O/U instead of spread)
            if (CfbSfPicks.TryGetValue(username, out var sf) && slates.FirstOrDefault(s => s.SlateNumber == 17) is { } slateSf)
            {
                var sfGames = CfpGames.Where(g => g.SlateIdx == 17).ToArray();
                for (int i = 0; i < Math.Min(sf.Length, sfGames.Length); i++) {
                    var team = sf[i] ? sfGames[i].Home : sfGames[i].Away;
                    if (i == 0) AddFirstPickOrOverUnder(username, user.Id, slateSf.Id, sfGames[0].Home, team);
                    else AddPick(league.Id, user.Id, slateSf.Id, team);
                }
            }

            // CFP Championship (slate 18): 1 pick total — Bob/Dana's is O/U instead of spread
            if (slates.FirstOrDefault(s => s.SlateNumber == 18) is { } slateFinal)
            {
                var finalGame = CfpGames.First(g => g.SlateIdx == 18);
                if (CfbFinalPicks.TryGetValue(username, out var final)) {
                    var team = final ? finalGame.Home : finalGame.Away;
                    AddFirstPickOrOverUnder(username, user.Id, slateFinal.Id, finalGame.Home, team);
                }
            }
        }

        if (picks.Count > 0)
        {
            db.CfbPicks.AddRange(picks);
            await db.SaveChangesAsync();
            Log.Information("DemoDataSeeder: seeded {Count} CFB picks", picks.Count);
        }
    }

    private async Task SeedCfbWeekConfigAsync()
    {
        if (await db.CfbSeasonWeekConfigs.AnyAsync(c => c.Season == CfbDemoSeason))
            return;

        var configs = new List<CfbSeasonWeekConfig>
        {
            // Regular season: ESPN weeks 1-13 → IV slates 1-13
            new() { Season = CfbDemoSeason, EspnWeekNumber =  1, IvLeagueWeekNumber =  1, WeekType = "Regular Season",           ScoringFormat = "Standard",      InScopeIvLeague = true,  WeekStartDate = new DateOnly(2025,  8, 23), WeekEndDate = new DateOnly(2025,  8, 30) },
            new() { Season = CfbDemoSeason, EspnWeekNumber =  2, IvLeagueWeekNumber =  2, WeekType = "Regular Season",           ScoringFormat = "Standard",      InScopeIvLeague = true,  WeekStartDate = new DateOnly(2025,  8, 30), WeekEndDate = new DateOnly(2025,  9,  6) },
            new() { Season = CfbDemoSeason, EspnWeekNumber =  3, IvLeagueWeekNumber =  3, WeekType = "Regular Season",           ScoringFormat = "Standard",      InScopeIvLeague = true,  WeekStartDate = new DateOnly(2025,  9,  6), WeekEndDate = new DateOnly(2025,  9, 13) },
            new() { Season = CfbDemoSeason, EspnWeekNumber =  4, IvLeagueWeekNumber =  4, WeekType = "Regular Season",           ScoringFormat = "Standard",      InScopeIvLeague = true,  WeekStartDate = new DateOnly(2025,  9, 13), WeekEndDate = new DateOnly(2025,  9, 20) },
            new() { Season = CfbDemoSeason, EspnWeekNumber =  5, IvLeagueWeekNumber =  5, WeekType = "Regular Season",           ScoringFormat = "Standard",      InScopeIvLeague = true,  WeekStartDate = new DateOnly(2025,  9, 20), WeekEndDate = new DateOnly(2025,  9, 27) },
            new() { Season = CfbDemoSeason, EspnWeekNumber =  6, IvLeagueWeekNumber =  6, WeekType = "Regular Season",           ScoringFormat = "Standard",      InScopeIvLeague = true,  WeekStartDate = new DateOnly(2025,  9, 27), WeekEndDate = new DateOnly(2025, 10,  4) },
            new() { Season = CfbDemoSeason, EspnWeekNumber =  7, IvLeagueWeekNumber =  7, WeekType = "Regular Season",           ScoringFormat = "Standard",      InScopeIvLeague = true,  WeekStartDate = new DateOnly(2025, 10,  4), WeekEndDate = new DateOnly(2025, 10, 11) },
            new() { Season = CfbDemoSeason, EspnWeekNumber =  8, IvLeagueWeekNumber =  8, WeekType = "Regular Season",           ScoringFormat = "Standard",      InScopeIvLeague = true,  WeekStartDate = new DateOnly(2025, 10, 11), WeekEndDate = new DateOnly(2025, 10, 18) },
            new() { Season = CfbDemoSeason, EspnWeekNumber =  9, IvLeagueWeekNumber =  9, WeekType = "Regular Season",           ScoringFormat = "Standard",      InScopeIvLeague = true,  WeekStartDate = new DateOnly(2025, 10, 18), WeekEndDate = new DateOnly(2025, 10, 25) },
            new() { Season = CfbDemoSeason, EspnWeekNumber = 10, IvLeagueWeekNumber = 10, WeekType = "Regular Season",           ScoringFormat = "Standard",      InScopeIvLeague = true,  WeekStartDate = new DateOnly(2025, 10, 25), WeekEndDate = new DateOnly(2025, 11,  1) },
            new() { Season = CfbDemoSeason, EspnWeekNumber = 11, IvLeagueWeekNumber = 11, WeekType = "Regular Season",           ScoringFormat = "Standard",      InScopeIvLeague = true,  WeekStartDate = new DateOnly(2025, 11,  1), WeekEndDate = new DateOnly(2025, 11,  8) },
            new() { Season = CfbDemoSeason, EspnWeekNumber = 12, IvLeagueWeekNumber = 12, WeekType = "Regular Season",           ScoringFormat = "Standard",      InScopeIvLeague = true,  WeekStartDate = new DateOnly(2025, 11,  8), WeekEndDate = new DateOnly(2025, 11, 15) },
            new() { Season = CfbDemoSeason, EspnWeekNumber = 13, IvLeagueWeekNumber = 13, WeekType = "Regular Season",           ScoringFormat = "Standard",      InScopeIvLeague = true,  WeekStartDate = new DateOnly(2025, 11, 15), WeekEndDate = new DateOnly(2025, 11, 22) },
            // ESPN week 14: Conference Championships → IV slate 14
            new() { Season = CfbDemoSeason, EspnWeekNumber = 14, IvLeagueWeekNumber = 14, WeekType = "Conference Championships", ScoringFormat = "Standard",      InScopeIvLeague = true,  WeekStartDate = new DateOnly(2025, 12,  5), WeekEndDate = new DateOnly(2025, 12,  7) },
            // ESPN week 15: Army-Navy (excluded)
            new() { Season = CfbDemoSeason, EspnWeekNumber = 15, IvLeagueWeekNumber = 99, WeekType = "Regular Season",           ScoringFormat = "NA",            InScopeIvLeague = false, WeekStartDate = new DateOnly(2025, 12, 13), WeekEndDate = new DateOnly(2025, 12, 13), Notes = "Army-Navy" },
            // ESPN week 16: CFP First Round → IV slate 15
            new() { Season = CfbDemoSeason, EspnWeekNumber = 16, IvLeagueWeekNumber = 15, WeekType = "FBS Playoff",              ScoringFormat = "NFLDivisional", InScopeIvLeague = true,  WeekStartDate = new DateOnly(2025, 12, 19), WeekEndDate = new DateOnly(2025, 12, 20) },
            // ESPN week 17: dead gap (excluded)
            new() { Season = CfbDemoSeason, EspnWeekNumber = 17, IvLeagueWeekNumber = 99, WeekType = "Dead",                     ScoringFormat = "NA",            InScopeIvLeague = false, WeekStartDate = new DateOnly(2025, 12, 25), WeekEndDate = new DateOnly(2025, 12, 27) },
            // ESPN week 18: CFP Quarterfinals → IV slate 16
            new() { Season = CfbDemoSeason, EspnWeekNumber = 18, IvLeagueWeekNumber = 16, WeekType = "FBS Playoff",              ScoringFormat = "NFLDivisional", InScopeIvLeague = true,  WeekStartDate = new DateOnly(2025, 12, 31), WeekEndDate = new DateOnly(2026,  1,  1) },
            // ESPN week 19: dead gap (excluded)
            new() { Season = CfbDemoSeason, EspnWeekNumber = 19, IvLeagueWeekNumber = 99, WeekType = "Dead",                     ScoringFormat = "NA",            InScopeIvLeague = false, WeekStartDate = new DateOnly(2026,  1,  3), WeekEndDate = new DateOnly(2026,  1,  5) },
            // ESPN week 20: CFP Semifinals → IV slate 17
            new() { Season = CfbDemoSeason, EspnWeekNumber = 20, IvLeagueWeekNumber = 17, WeekType = "FBS Playoff",              ScoringFormat = "NFLConference", InScopeIvLeague = true,  WeekStartDate = new DateOnly(2026,  1,  8), WeekEndDate = new DateOnly(2026,  1,  9) },
            // ESPN week 21: CFP Championship → IV slate 18
            new() { Season = CfbDemoSeason, EspnWeekNumber = 21, IvLeagueWeekNumber = 18, WeekType = "FBS Playoff",              ScoringFormat = "NFLSuperBowl",  InScopeIvLeague = true,  WeekStartDate = new DateOnly(2026,  1, 19), WeekEndDate = new DateOnly(2026,  1, 19) },
        };

        // SpreadLockDatetime is required (NOT NULL) — demo rows don't need real-world accuracy,
        // just a deterministic, non-default value; midnight UTC on the week's own start date.
        foreach (var config in configs)
            config.SpreadLockDatetime = DateTime.SpecifyKind(config.WeekStartDate.ToDateTime(TimeOnly.MinValue), DateTimeKind.Utc);

        db.CfbSeasonWeekConfigs.AddRange(configs);
        await db.SaveChangesAsync();
        Log.Information("DemoDataSeeder: seeded {Count} CfbSeasonWeekConfig rows for {Season}", configs.Count, CfbDemoSeason);
    }
}
