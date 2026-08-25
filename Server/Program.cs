using FourPlayWebApp.Server.Data;
using FourPlayWebApp.Server.Jobs;
using FourPlayWebApp.Server.Models.Identity;
using FourPlayWebApp.Server.Services;
using FourPlayWebApp.Server.Services.Interfaces;
using FourPlayWebApp.Server.Services.Repositories;
using FourPlayWebApp.Server.Services.Repositories.Interfaces;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.AspNetCore.RateLimiting;
using Quartz;
using Serilog;
using Serilog.Formatting.Compact;
using System.Text;
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

// Serilog
builder.Host.UseSerilog((context, services, configuration) => configuration
    .WriteTo.Console(new CompactJsonFormatter()).Enrich.FromLogContext()
    .MinimumLevel.Override("Quartz", Serilog.Events.LogEventLevel.Warning)
    .MinimumLevel.Override("Microsoft.AspNetCore", Serilog.Events.LogEventLevel.Warning)
    .MinimumLevel.Override("Microsoft.EntityFrameworkCore.Database.Command", Serilog.Events.LogEventLevel.Warning)
    .MinimumLevel.Override("System.Net.Http.HttpClient", Serilog.Events.LogEventLevel.Warning)
    .ReadFrom.Services(services));

// Add services to the container.
var rawConnectionString = builder.Configuration.GetConnectionString("POSTGRES_CONNECTION_STRING") ??
                          throw new InvalidOperationException("Connection string 'POSTGRES_CONNECTION_STRING' not found.");

// Support both postgres:// URL format and Npgsql key=value format
var connectionString = FourPlayWebApp.Server.Infrastructure.PostgresConnectionString.Normalize(rawConnectionString);
builder.Services.AddOptions();
builder.Services.AddControllersWithViews();
#region Email
// Validate critical configuration early (fail fast)
var emailUser = Environment.GetEnvironmentVariable("FOURPLAY_EMAIL_USER");
var emailPass = Environment.GetEnvironmentVariable("FOURPLAY_EMAIL_PASS");
if (string.IsNullOrWhiteSpace(emailUser) || string.IsNullOrWhiteSpace(emailPass))
{
    Log.Error("Missing required email configuration: FOURPLAY_EMAIL_USER and FOURPLAY_EMAIL_PASS must be set. Aborting startup.");
    throw new InvalidOperationException("Missing required email configuration: FOURPLAY_EMAIL_USER and FOURPLAY_EMAIL_PASS must be set.");
}
// Add Email Sender
builder.Services.AddTransient<IEmailSender, GoogleEmailSender>();
builder.Services.AddTransient<IEmailSender<ApplicationUser>, GoogleEmailSender>();
#endregion
#region Odds and Scores
// site.api.espn.com is an undocumented, unofficial endpoint (ESPN's own frontend API, not a
// supported developer product) that started rejecting our honest identity with 403s (verified
// 2026-08-05 — see PR discussion). The default HttpClient sends no User-Agent at all, and
// neither an empty one nor a normal branded one ("IVLeagueApp/1.0...") gets through; only the
// unmodified default signature of a handful of common HTTP libraries does. This is a stopgap,
// not a stable integration — it can stop working without notice at any time, since it's
// leaning on an access-control gap rather than a sanctioned API contract. Tracked as a real
// follow-up: move off this endpoint onto a licensed sports-data provider.
const string espnUserAgent = "curl/8.14.1";
builder.Services.AddHttpClient<IEspnCoreOddsService, EspnCoreOddsService>(x => {
    x.BaseAddress = new Uri("https://sports.core.api.espn.com");
    x.DefaultRequestHeaders.UserAgent.ParseAdd(espnUserAgent);
});
builder.Services.AddHttpClient<IEspnApiService, EspnApiService>(x => {
    x.BaseAddress = new Uri("http://site.api.espn.com");
    x.DefaultRequestHeaders.UserAgent.ParseAdd(espnUserAgent);
});
builder.Services.AddHttpClient<ICfbApiService, CfbApiService>(x => {
    x.BaseAddress = new Uri("http://site.api.espn.com");
    x.DefaultRequestHeaders.UserAgent.ParseAdd(espnUserAgent);
});
var isDemoMode = builder.Configuration["DEMO_MODE"] == "true";
var isDemoReplayMode = builder.Configuration["DEMO_REPLAY_MODE"] == "true";
var seedsDemoData = isDemoMode || isDemoReplayMode;

if (isDemoReplayMode)
{
    // frizat-703.6: one ReplayCacheService instance backs BOTH sports' live-data endpoints and
    // SSE streams, advancing on an explicit test-only trigger instead of a timer — see
    // ReplayCacheService's own doc comment.
    builder.Services.AddSingleton(sp => ReplayCacheService.LoadFromFixtureFiles(
        Path.Combine(sp.GetRequiredService<IWebHostEnvironment>().ContentRootPath, "..")));
    builder.Services.AddSingleton<IEspnCacheService>(sp => sp.GetRequiredService<ReplayCacheService>());
    builder.Services.AddSingleton<ICfbCacheService>(sp => sp.GetRequiredService<ReplayCacheService>());
}
else if (isDemoMode)
{
    builder.Services.AddSingleton<IEspnCacheService, DemoEspnCacheService>();
    builder.Services.AddSingleton<ICfbCacheService, DemoCfbCacheService>();
}
else {
    builder.Services.AddSingleton<IEspnCacheService, EspnCacheService>();
    builder.Services.AddSingleton<ICfbCacheService, CfbCacheService>();
}

if (seedsDemoData)
    builder.Services.AddScoped<DemoDataSeeder>();
// Add HttpClient for Gridiron Uniforms and register Jersey cache service
builder.Services.AddHttpClient<IJerseyCacheService, JerseyCacheService>(c => {
    c.BaseAddress = new Uri("https://www.gridiron-uniforms.com/GUD/");
    // Some servers dislike aggressive headers; set a reasonable user agent
    c.DefaultRequestHeaders.UserAgent.ParseAdd("IVLeagueJerseyCache/1.0");
    c.DefaultRequestHeaders.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("text/html"));
});
#endregion
builder.Services.AddDbContextFactory<ApplicationDbContext>(options => {
    options.UseNpgsql(connectionString);
    if (builder.Environment.IsDevelopment()) {
        options.EnableSensitiveDataLogging();
        options.EnableDetailedErrors();
    }
    // Suppress the EF Core 9 startup check that blocks migration application.
    // Pending migrations are applied immediately below via db.Database.Migrate().
    options.ConfigureWarnings(w =>
        w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning));
});
builder.Services.AddDatabaseDeveloperPageExceptionFilter();


builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
    {
        options.User.RequireUniqueEmail = true;
        options.SignIn.RequireConfirmedEmail = true;   // block until confirmed
        options.SignIn.RequireConfirmedAccount = true; // ASP.NET 6+ templates use this
        // --- Lockout settings ---
        options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15); // lockout duration
        options.Lockout.MaxFailedAccessAttempts = 5; // number of allowed failed attempts
        options.Lockout.AllowedForNewUsers = true; // apply lockout to new users
    })
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddDefaultTokenProviders();
// JWT (validated server-side). We'll read the token from a HttpOnly cookie.
var jwtSection = builder.Configuration.GetSection("Jwt");
if (string.IsNullOrWhiteSpace(jwtSection["Key"]))
{
    Log.Error("Missing required JWT configuration: Jwt:Key must be set. Aborting startup.");
    throw new InvalidOperationException("Missing required JWT configuration: Jwt:Key must be set.");
}
var keyBytes = Encoding.UTF8.GetBytes(jwtSection["Key"]!);

builder.Services
    .AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme    = JwtBearerDefaults.AuthenticationScheme;
    })
    .AddJwtBearer(JwtBearerDefaults.AuthenticationScheme, options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidIssuer = jwtSection["Issuer"],
            ValidAudience = jwtSection["Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(keyBytes),
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateIssuerSigningKey = true,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromMinutes(1)
        };

        // Read JWT from HttpOnly cookie "AuthToken"
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                if (context.Request.Cookies.TryGetValue("AuthToken", out var token))
                {
                    context.Token = token;
                }
                return Task.CompletedTask;
            }
        };
    });

builder.Services.AddAuthorizationBuilder()
    .AddPolicy("Admin", p => p.RequireRole("Administrator"));

// Register Refresh Token Service
builder.Services.AddScoped<IRefreshTokenService, RefreshTokenService>();

// Register JwtTokenService
builder.Services.AddScoped<IJwtTokenService, JwtTokenService>();

// Rate limiting configuration for auth endpoints
builder.Services.AddRateLimiter(options =>
{
    // Return 429 when limits are exceeded
    options.RejectionStatusCode = 429;

    // Login endpoint: 5 requests per minute per IP
    options.AddFixedWindowLimiter("auth", opt =>
    {
        opt.PermitLimit = 5;
        opt.Window = TimeSpan.FromMinutes(1);
        opt.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
        opt.QueueLimit = 0;
    });

    // Refresh endpoint: reasonably permissive but limited
    options.AddFixedWindowLimiter("refresh", opt =>
    {
        opt.PermitLimit = 30;
        opt.Window = TimeSpan.FromMinutes(1);
        opt.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
        opt.QueueLimit = 0;
    });

    // Registration endpoint: 3 attempts per 5 minutes per IP
    options.AddFixedWindowLimiter("register", opt =>
    {
        opt.PermitLimit = 3;
        opt.Window = TimeSpan.FromMinutes(5);
        opt.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
        opt.QueueLimit = 0;
    });

    // Forgot password endpoint: 3 attempts per hour per IP
    options.AddFixedWindowLimiter("forgot", opt =>
    {
        opt.PermitLimit = 3;
        opt.Window = TimeSpan.FromHours(1);
        opt.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
        opt.QueueLimit = 0;
    });

    // Public, unauthenticated, no-side-effect endpoints (e.g. /api/version): generous but bounded,
    // so an unthrottled scanner/bot can't hit them at unlimited volume the way every other
    // anonymous endpoint in this app is already protected from.
    options.AddFixedWindowLimiter("public", opt =>
    {
        opt.PermitLimit = 60;
        opt.Window = TimeSpan.FromMinutes(1);
        opt.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
        opt.QueueLimit = 0;
    });
});


builder.Services.AddAuthorization();
builder.Services.AddMemoryCache();

// CORS — allow configured origins (comma-separated ALLOWED_ORIGINS env var).
// Fails fast in non-Development when ALLOWED_ORIGINS is unset to prevent wildcard CORS in prod.
var allowedOrigins = FourPlayWebApp.Server.Infrastructure.StartupValidation.ParseAndValidateCorsOrigins(
    Environment.GetEnvironmentVariable("ALLOWED_ORIGINS"),
    builder.Environment.IsDevelopment());
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        if (allowedOrigins.Length > 0)
            policy.WithOrigins(allowedOrigins).AllowAnyHeader().AllowAnyMethod().AllowCredentials();
        else
            policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod(); // Dev only — validated above
    });
});
// Add Invitation Service
builder.Services.AddScoped<IInvitationService, InvitationService>();
builder.Services.AddScoped<ILeagueInviteLinkService, LeagueInviteLinkService>();
builder.Services.AddScoped<ILeagueMembershipInviteService, LeagueMembershipInviteService>();

builder.Services.AddScoped<ISpreadCalculatorBuilder, SpreadCalculatorBuilder>();
builder.Services.AddSingleton<ILeaderboardService, LeaderboardService>();
builder.Services.AddScoped<ICfbLeaderboardService, CfbLeaderboardService>();
builder.Services.AddSingleton<ILeagueRepository, LeagueRepository>();
builder.Services.AddScoped<ICfbRepository, CfbRepository>();
builder.Services.AddScoped<ICfbPicksRepository, CfbPicksRepository>();
builder.Services.AddSingleton<INflCurrentWeekService, NflCurrentWeekService>();
// Injectable clock for NflSpreadJob/CfbSpreadJob's lock-time write guard (SpreadLockGuard) — lets
// tests control "now" exactly instead of depending on the real wall clock.
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddScoped<ICfbCurrentSlateService, CfbCurrentSlateService>();
builder.Services.AddSingleton<ICfbLiveScoreFetcher, CfbLiveScoreFetcher>();
builder.Services.AddScoped<NflSpreadScheduleSource>();
builder.Services.AddScoped<CfbSpreadScheduleSource>();
// Register job observer for observability
builder.Services.AddSingleton<IJobObserverService, JobObserverService>();

#region Quartz
// Quartz
builder.Services.AddScoped<IJob, NflScoresJob>();
builder.Services.AddScoped<IJob, NflSpreadJob>();
builder.Services.AddScoped<IJob, NflSpreadSchedulerJob>();
builder.Services.AddScoped<IJob, UserManagerJob>();
builder.Services.AddScoped<IJob, CfbSlateSeederJob>();
builder.Services.AddScoped<IJob, CfbSpreadSchedulerJob>();
builder.Services.AddScoped<IJob, CfbRankingCaptureJob>();
builder.Services.AddScoped<IJob, CfbSpreadJob>();
builder.Services.AddScoped<IJob, CfbScoresJob>();
builder.Services.AddQuartz(q => {
    // In DEMO_MODE/DEMO_REPLAY_MODE fire in 5s so seeding completes before e2e tests start;
    // otherwise fire 2 min after startup to avoid slowing cold boot.
    var userManagerDelay = seedsDemoData ? 5 : 120;
    q.ScheduleJob<UserManagerJob>(trigger => trigger
        .WithIdentity("User Manager")
        .WithDescription("Manages initial user admin (mark)")
        .StartAt(DateBuilder.FutureDate(userManagerDelay, IntervalUnit.Second))
    );

    // CFB Slate Seeder — idempotent, runs Monday 5am CST to catch new seasons. Slate-seeding only
    // — spread-lock trigger scheduling is CfbSpreadSchedulerJob below (frizat-pxy follow-on:
    // structurally identical to NflSpreadSchedulerJob, its own cadence, not fused into this job).
    // Registered unconditionally (like UserManagerJob) — it only manages internal slate/date
    // structure, never pulls live scores or spreads, so it's safe in demo mode.
    q.ScheduleJob<CfbSlateSeederJob>(trigger => trigger
        .WithIdentity("CFB Slate Seeder Startup")
        .WithDescription("Seeds CFB slate dates for the current season")
        .StartAt(DateBuilder.FutureDate(60, IntervalUnit.Second))
    );
    q.ScheduleCstCronJob<CfbSlateSeederJob>("CFB Slate Seeder", "Seeds CFB slate dates for the current season", "0 0 5 ? * MON");

    // frizat: every job below this line pulls LIVE data from ESPN and writes it into the same
    // tables DemoDataSeeder owns — NflScoresJob in particular loops currentYear-2..+1, which
    // always includes whatever season the demo seeder fictionally populates. NflScores' unique
    // index is (Season, NflWeek, HomeTeam), not a real per-game key, so a live upsert doesn't even
    // reliably collide with the demo's row for the "same" game if the two sources disagree on
    // which team was home — it just adds a second, conflicting row instead. Confirmed in practice:
    // this is exactly what corrupted the local demo DB's NFL Week 18 data and leaderboard totals
    // over the course of a multi-day session with these crons left unguarded. None of these jobs
    // have any reason to run against a demo dataset that's supposed to be fully and only owned by
    // the seeder — skip all of them (both sports, symmetrically) when seeding demo data.
    if (!seedsDemoData)
    {
        // NFL Spreads — frizat-pxy: NflSpreadJob has no fixed trigger of its own anymore.
        // NflSpreadSchedulerJob reads NflSeasonWeekConfig.SpreadLockDatetime and registers a precise
        // one-time trigger per upcoming week, replacing the old Thursday-2pm/Christmas-Eve crons
        // (superseded docs/plans/SPREAD_GRAB_PLAN.md heuristic).
        q.ScheduleJob<NflSpreadSchedulerJob>(trigger => trigger
            .WithIdentity("NFL Spread Scheduler Startup")
            .WithDescription("Registers per-week NFL spread-lock triggers from NflSeasonWeekConfig")
            .StartAt(DateBuilder.FutureDate(60, IntervalUnit.Second))
        );
        q.ScheduleCstCronJob<NflSpreadSchedulerJob>("NFL Spread Scheduler Daily", "Daily catch-up pass for NFL spread-lock triggers", "0 0 6 * * ?");

        // NFL Scores
        q.ScheduleCstCronJob<NflScoresJob>("NFL Scores Thu 10am", "Fetches NFL scores Thursday morning at 10am CST", "0 0 10 ? * THU");
        q.ScheduleCstCronJob<NflScoresJob>("NFL Scores Fri 1am", "Fetches NFL scores early Friday at 1am CST", "0 0 1 ? * FRI");
        q.ScheduleCstCronJob<NflScoresJob>("NFL Scores Sun 12:30pm", "Fetches NFL scores just before Sunday early games at 12:30pm CST", "0 30 12 ? * SUN");
        q.ScheduleCstCronJob<NflScoresJob>("NFL Scores Sun 4:30pm", "Fetches NFL scores during Sunday late games at 4:30pm CST", "0 30 16 ? * SUN");
        q.ScheduleCstCronJob<NflScoresJob>("NFL Scores Sun 7:40pm", "Fetches NFL scores during Sunday evening games at 7:40pm CST", "0 40 19 ? * SUN");
        q.ScheduleCstCronJob<NflScoresJob>("NFL Scores Mon 1am", "Fetches NFL scores early Monday after SNF at 1am CST", "0 0 1 ? * MON");
        q.ScheduleCstCronJob<NflScoresJob>("NFL Scores Tue 1am", "Fetches NFL scores early Tuesday after MNF at 1am CST", "0 0 1 ? * TUE");

        // CFB Rankings — CFB-only, no NFL equivalent (rank/eligibility is a CFB-specific concept).
        // Same cadence as the slate seeder: capture rankings as soon as a week's schedule is known,
        // not gated on that week's spread lock like CfbSpreadJob's own (later) ranking capture is.
        q.ScheduleJob<CfbRankingCaptureJob>(trigger => trigger
            .WithIdentity("CFB Ranking Capture Startup")
            .WithDescription("Captures CFB AP rankings as soon as each week's schedule is known")
            .StartAt(DateBuilder.FutureDate(60, IntervalUnit.Second))
        );
        q.ScheduleCstCronJob<CfbRankingCaptureJob>("CFB Ranking Capture", "Captures CFB AP rankings as soon as each week's schedule is known", "0 0 5 ? * MON");

        // CFB Spreads — mirrors NflSpreadSchedulerJob above exactly: CfbSpreadJob has no fixed trigger
        // of its own; CfbSpreadSchedulerJob reads CfbSeasonWeekConfig.SpreadLockDatetime and registers
        // a precise one-time trigger per upcoming week, with data-driven catch-up for past-due weeks.
        // Same daily cadence as NFL — previously CFB's catch-up only ran weekly (piggybacked on the
        // slate seeder), 7x less often than NFL for no sport-specific reason.
        q.ScheduleJob<CfbSpreadSchedulerJob>(trigger => trigger
            .WithIdentity("CFB Spread Scheduler Startup")
            .WithDescription("Registers per-week CFB spread-lock triggers from CfbSeasonWeekConfig")
            .StartAt(DateBuilder.FutureDate(60, IntervalUnit.Second))
        );
        q.ScheduleCstCronJob<CfbSpreadSchedulerJob>("CFB Spread Scheduler Daily", "Daily catch-up pass for CFB spread-lock triggers", "0 0 6 * * ?");

        // CFB Scores — Saturday noon, 4pm, 8pm, midnight CST + Sunday 6am CST (covers all kickoff windows)
        q.ScheduleCstCronJob<CfbScoresJob>("CFB Scores Sat Noon", "Fetches CFB scores at Saturday noon kickoff window", "0 0 12 ? * SAT");
        q.ScheduleCstCronJob<CfbScoresJob>("CFB Scores Sat 4pm", "Fetches CFB scores at Saturday afternoon kickoff window", "0 0 16 ? * SAT");
        q.ScheduleCstCronJob<CfbScoresJob>("CFB Scores Sat 8pm", "Fetches CFB scores at Saturday evening kickoff window", "0 0 20 ? * SAT");
        q.ScheduleCstCronJob<CfbScoresJob>("CFB Scores Sat Midnight", "Fetches CFB final scores late Saturday night", "0 0 0 ? * SUN");
        q.ScheduleCstCronJob<CfbScoresJob>("CFB Scores Sun 6am", "Fetches CFB overnight final scores Sunday morning", "0 0 6 ? * SUN");
    }
});


// Quartz.Extensions.Hosting allows you to fire background service that handles scheduler lifecycle
builder.Services.AddQuartzHostedService(options => {
    // when shutting down we want jobs to complete gracefully
    options.WaitForJobsToComplete = true;
});
#endregion

var app = builder.Build();

try
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    var pending = db.Database.GetPendingMigrations().ToList();

    if (app.Environment.IsDevelopment()) {
        // Local machine + demo stack: auto-apply for convenience, matches CLAUDE.md.
        if (pending.Count > 0)
            Log.Information("Applying {Count} pending migration(s): {Names}", pending.Count, pending);
        else
            Log.Information("Database is up to date, no migrations needed");
        db.Database.Migrate();
    } else if (pending.Count > 0) {
        // Deployed environments (Railway dev/prod): migrations are applied by the "DB Migrate"
        // GitHub Actions workflow (.github/workflows/migrate.yml) before this deploy is allowed
        // to happen at all (Railway's checkSuites gate blocks the deploy on that job failing).
        // Reaching pending migrations here means that gate was bypassed or the job never ran —
        // fail loudly instead of silently serving traffic against a stale schema, which is
        // exactly what happened silently for days before this check existed.
        Log.Fatal("{Count} pending migration(s) not applied: {Names}. The DB Migrate workflow " +
            "should have applied these before this deploy started — refusing to serve traffic " +
            "against a stale schema.", pending.Count, pending);
        throw new InvalidOperationException($"{pending.Count} pending migration(s) not applied: {string.Join(", ", pending)}");
    } else {
        Log.Information("Database is up to date, no migrations needed");
    }
}
catch (Exception ex)
{
    Log.Error(ex, "Error Upgrading DB");
    throw;
}

if (seedsDemoData)
{
    using var demoScope = app.Services.CreateScope();
    await demoScope.ServiceProvider.GetRequiredService<DemoDataSeeder>().SeedAsync();
}


// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseMigrationsEndPoint();
}
else
{
    app.UseExceptionHandler("/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseCors();

// Enable rate limiter middleware
app.UseRateLimiter();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers(); // or map minimal

app.MapFallbackToFile("index.html");

app.Run();
