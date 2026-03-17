# Backend Code Quality Review

**Reviewed:** 2026-03-05
**Scope:** `Server/` controllers, services, jobs, repositories · `Shared/` models
**Branch:** `feat/react-migration`

## Executive Summary

The ASP.NET Core 9 backend is well-structured: HttpOnly JWT cookies, interface-driven services, `IDbContextFactory` usage, Quartz.NET for background jobs, and a clean repository pattern. However three critical issues require attention before the next production deployment: a hardcoded admin email baked into a background job, missing role authorization on destructive bulk-data endpoints, and a thread-safety bug in the singleton `SpreadCalculatorBuilder`. Several high-severity issues around credential logging, N+1 queries, and identity verification on `ChangePassword` should follow immediately.

---

## Critical

### 1. Hardcoded Admin Email and Username in Production Job
- **File:** `Server/Jobs/UserManagerJob.cs:31,79`
- `const string adminUserEmail = "markmjohnson@gmail.com"` and `UserName = "frizat"` are hardcoded. Changing the admin requires a code change + redeploy.
- **Fix:** Read from env vars (e.g., `ADMIN_EMAIL`, `ADMIN_USERNAME`) — same pattern already used for `ADMIN_PASSWORD` on line 83.

### 2. No Role Authorization on Destructive Bulk-Data Endpoints
- **File:** `Server/Controllers/LeagueController.cs:185–248, 330–345`
- `DELETE /api/league/scores`, `DELETE /api/league/spreads`, `DELETE /api/league/picks`, and the bulk upsert POSTs are guarded only by `[Authorize]`. Any authenticated user can delete all NFL scores or spreads.
- **Fix:** Add `[Authorize(Roles = "Administrator")]` to all bulk write and delete action methods.

### 3. Thread-Safety Bug: Singleton `SpreadCalculatorBuilder` Has Shared Mutable State
- **File:** `Server/Services/SpreadCalculatorBuilder.cs:9–11` · `Server/Program.cs:193`
- Registered as **Singleton** but stores `_leagueId`, `_week`, `_season` as instance fields. Concurrent requests will overwrite each other's values, silently computing results with the wrong parameters.
- **Fix:** Register as `Scoped`, or have `BuildAsync` accept parameters directly instead of relying on mutable builder state.

---

## High

### 4. Connection String Logged in Plain Text at Startup
- **File:** `Server/Program.cs:35`
- `Log.Information("DB {ConnectionString}", connectionString)` writes the full PostgreSQL connection string (including password) to Serilog's console sink. Exposes credentials in any log aggregation pipeline.
- **Fix:** Remove the line. If needed for debugging, log only the server hostname.

### 5. `EnableSensitiveDataLogging` Active in All Environments
- **File:** `Server/Program.cs:74`
- EF Core logs all SQL parameter values (user IDs, emails, pick data) in every environment.
- **Fix:** Wrap in `if (builder.Environment.IsDevelopment())`.

### 6. `ChangePassword` Does Not Verify Caller Identity Matches Target
- **File:** `Server/Controllers/AuthController.cs:323–341`
- Accepts `model.Email` from the request body and targets that user. Any authenticated user can probe or attempt to change another user's password.
- **Fix:** Derive the target user from `User.FindFirstValue(ClaimTypes.NameIdentifier)` (the JWT claim), not from the request body.

### 7. `confirm-email` Logs Security Token as UserId (Wrong Field)
- **File:** `Server/Controllers/AuthController.cs:185`
- `logger.LogError("...{UserId}...", request.Token, ...)` logs the confirmation token where `request.UserId` is intended — wrong data logged under a misleading label, and a sensitive token written to logs.
- **Fix:** Change second argument to `request.UserId`.

### 8. N+1 Query Pattern in Leaderboard Calculation
- **File:** `Server/Services/LeaderboardService.cs:42–45`
- Per user × per week, `CalculatePicks` calls `GetUserNflPicksAsync` — a separate DB query per pair. For 10 users × 18 weeks = 180 queries per leaderboard build before cache warms.
- **Fix:** Load all picks for the league/season in one query, partition in-memory by user and week.

### 9. N+1 Query Pattern in `GetUsers`
- **File:** `Server/Controllers/LeagueController.cs:146–158`
- After loading all users, `userManager.GetRolesAsync(u)` is called per user in a loop — one DB round-trip per user.
- **Fix:** Load all user-role pairs in a single joined query, then match in-memory.

### 10. Fire-and-Forget `Task.Run` Silently Swallows Exceptions
- **File:** `Server/Controllers/JerseyController.cs:50`
- `_ = Task.Run(() => jerseyCacheService.RefreshAsync(...))` — no error handling, no cancellation, no lifetime awareness. Failures are invisible.
- **Fix:** Use a proper `IHostedService`-backed background queue, or attach a `.ContinueWith` to log failures.

### 11. `RefreshTokenService` Injects `ApplicationDbContext` Directly (Not Factory)
- **File:** `Server/Services/RefreshTokenService.cs:12`
- All other services use `IDbContextFactory<ApplicationDbContext>`. Direct injection creates lifetime mismatch risk when used from jobs or singleton-scoped callers.
- **Fix:** Inject `IDbContextFactory<ApplicationDbContext>` and create/dispose contexts per operation.

---

## Medium

### 12. `SpreadCalculatorBuilder` Cache Key for Juice Ignores Season
- **File:** `Server/Services/SpreadCalculatorBuilder.cs:58`
- `"juice_{0}"` keyed only on `leagueId`. Different seasons may have different juice settings; first season's values persist for 1 hour.
- **Fix:** Key as `"juice_{0}_{1}"` using `leagueId` and `season`.

### 13. `NflScoresJob` Makes ~96 Sequential External HTTP Calls Per Run
- **File:** `Server/Jobs/NFLScoresJob.cs:19–48`
- 4 years × ~24 week combinations = up to 96 sequential ESPN API calls per job run, risking timeouts and rate-limiting.
- **Fix:** Use `Parallel.ForEachAsync` with a bounded degree of parallelism; break inner loop early when ESPN returns null.

### 14. N+1 Inside `UpsertNflScoresAsync` / `UpsertNflWeeksAsync`
- **File:** `Server/Services/Repositories/LeagueRepository.cs:105–123, 164–185`
- Each item in the list triggers a `FirstOrDefaultAsync` SELECT before insert/update. For a 16-game week = 16 individual SELECTs.
- **Fix:** Load all existing rows for the season/week in one query before the loop, match in-memory.

### 15. `StartupJob` Blocks a Quartz Worker Thread for One Minute
- **File:** `Server/Jobs/StartupJob.cs:8`
- `await Task.Delay(TimeSpan.FromMinutes(1))` holds a Quartz thread pool worker for 60 seconds. The job has no trigger assigned — it is effectively dead code with a hazardous delay.
- **Fix:** Remove `StartupJob`. Use scheduled job `StartAt` offsets if sequencing is needed.

### 16. `EspnCacheService` Uses `Console.WriteLine` Instead of Logger
- **File:** `Server/Services/EspnCacheService.cs:69`
- Bypasses Serilog entirely; errors won't appear in structured logs or any aggregation pipeline.
- **Fix:** Inject `ILogger<EspnCacheService>` and use `logger.LogError(ex, ...)`.

### 17. `CalculateUserTotals` Exposed on the Public Service Interface
- **File:** `Server/Services/Interfaces/ILeaderboardService.cs:7`
- An internal implementation step of `BuildLeaderboard` is part of the public interface, allowing callers to invoke it out of sequence.
- **Fix:** Remove from the interface; make `private` in `LeaderboardService`.

### 18. `GetLeagueInfo` Throws Unhandled 500 on Missing League
- **File:** `Server/Controllers/LeagueController.cs:31–38`
- Repository uses `.FirstAsync()` which throws `InvalidOperationException` if not found → unhandled 500 instead of 404.
- **Fix:** Use `FirstOrDefaultAsync`, return null from repository, return `NotFound()` from controller.

### 19. `AddPicks` Has TOCTOU Race Condition on Count Check
- **File:** `Server/Controllers/LeagueController.cs:321–327`
- Check-and-insert is not atomic. Concurrent requests from the same user can both pass the count check before either insert completes.
- **Fix:** Wrap in a DB transaction with re-check inside, or use an app-level lock keyed on `userId + leagueId + week`.

### 20. Unnecessary Round-Trip When Adding Picks (All Weeks Loaded for One)
- **File:** `Server/Controllers/LeagueController.cs:296`
- `repo.GetNflWeeksAsync(season)` loads all weeks just to find one `NflWeekId` on every pick submission.
- **Fix:** Add `GetNflWeekAsync(int season, int week)` that fetches the specific week only.

---

## Low

### 21. Commented-Out Code and Unresolved TODOs Throughout
- `Server/Jobs/NFLScoresJob.cs:22,37` — `// TODO: how do i know the year?`
- `Server/Jobs/NFLSpreadJob.cs:46,50` — placeholder `Log.Error("Error")` with `"FK"` string check
- `Server/Controllers/LeagueController.cs:496–497, 63–72` — commented-out blocks
- `Server/Controllers/EspnController.cs:28–44` — commented-out odds endpoints
- `Server/Controllers/AuthController.cs:342–353` — commented-out endpoint
- `Server/Jobs/StartupJob.cs:10–12` — commented-out trigger
- `Server/Services/SpreadCalculator.cs:15,28` — `//TODO: Add Caching`
- **Fix:** Resolve or create tracked issues. Delete commented-out code.

### 22. `InvitationService` and `NFLSpreadJob` Use Static `Serilog.Log`
- `Server/Services/InvitationService.cs` · `Server/Jobs/NFLSpreadJob.cs`
- Inconsistent with rest of codebase; untestable.
- **Fix:** Inject `ILogger<T>` via constructor.

### 23. No Input Size Cap on Bulk Upsert Endpoints
- **File:** `Server/Controllers/LeagueController.cs:185–248`
- No maximum enforced on `List<NflScores>` / `IEnumerable<NflSpreads>` payloads.
- **Fix:** Validate count and return `BadRequest` if above a reasonable limit (e.g., 300).

### 24. Refresh Tokens Accumulate Forever (No Cleanup Job)
- **File:** `Server/Services/RefreshTokenService.cs`
- No scheduled job deletes expired or revoked tokens. Table will grow unbounded.
- **Fix:** Add a periodic Quartz job: `DELETE WHERE Expires < NOW() OR Revoked IS NOT NULL`.

### 25. `MissingPicksJob` Uses `MissingFieldException` for Missing Env Var
- **File:** `Server/Jobs/MissingPicksJob.cs:15`
- Wrong exception type; error surfaces at job instantiation, not at startup.
- **Fix:** Validate `APP_URL` in `Program.cs` at startup; use `InvalidOperationException`.

### 26. `UserManagerJob` Injects `ApplicationDbContext` Directly
- **File:** `Server/Jobs/UserManagerJob.cs:15`
- Same lifetime concern as `RefreshTokenService`.
- **Fix:** Inject `IDbContextFactory<ApplicationDbContext>`.

### 27. `LeaderboardService` Registered as Singleton (Fragile for Future Dependencies)
- **File:** `Server/Program.cs:194–195`
- Currently works (all its deps are also singletons), but any future scoped dependency added will cause a captive-dependency bug silently.
- **Fix:** Register as `Transient` or `Scoped`; rely on controller-level caching for performance.

---

## Summary Table

| # | Severity | Area | Location |
|---|----------|------|----------|
| 1 | **Critical** | Security/Config | `Jobs/UserManagerJob.cs:31,79` |
| 2 | **Critical** | Authorization | `Controllers/LeagueController.cs:185–248` |
| 3 | **Critical** | Thread Safety | `Services/SpreadCalculatorBuilder.cs:9–11` |
| 4 | High | Security | `Program.cs:35` |
| 5 | High | Security | `Program.cs:74` |
| 6 | High | Authorization | `Controllers/AuthController.cs:323–341` |
| 7 | High | Logging Bug | `Controllers/AuthController.cs:185` |
| 8 | High | Performance | `Services/LeaderboardService.cs:42–45` |
| 9 | High | Performance | `Controllers/LeagueController.cs:146–158` |
| 10 | High | Reliability | `Controllers/JerseyController.cs:50` |
| 11 | High | Correctness | `Services/RefreshTokenService.cs:12` |
| 12 | Medium | Correctness | `Services/SpreadCalculatorBuilder.cs:58` |
| 13 | Medium | Performance | `Jobs/NFLScoresJob.cs:19–48` |
| 14 | Medium | Performance | `Repositories/LeagueRepository.cs:105,164` |
| 15 | Medium | Correctness | `Jobs/StartupJob.cs:8` |
| 16 | Medium | Observability | `Services/EspnCacheService.cs:69` |
| 17 | Medium | Architecture | `Services/Interfaces/ILeaderboardService.cs:7` |
| 18 | Medium | Error Handling | `Controllers/LeagueController.cs:31–38` |
| 19 | Medium | Concurrency | `Controllers/LeagueController.cs:321–327` |
| 20 | Medium | Performance | `Controllers/LeagueController.cs:296` |
| 21 | Low | Code Quality | Multiple files |
| 22 | Low | Consistency | `Services/InvitationService.cs`, `Jobs/NFLSpreadJob.cs` |
| 23 | Low | Validation | `Controllers/LeagueController.cs:185–248` |
| 24 | Low | Maintenance | `Services/RefreshTokenService.cs` |
| 25 | Low | Config | `Jobs/MissingPicksJob.cs:15` |
| 26 | Low | Correctness | `Jobs/UserManagerJob.cs:15` |
| 27 | Low | Architecture | `Program.cs:194–195` |
