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
using Quartz.Impl.AdoJobStore;
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
var connectionString = rawConnectionString.StartsWith("postgres://") || rawConnectionString.StartsWith("postgresql://")
    ? ConvertPostgresUrl(rawConnectionString)
    : rawConnectionString;

static string ConvertPostgresUrl(string url)
{
    var uri = new Uri(url);
    var userInfo = uri.UserInfo.Split(':');
    var username = userInfo[0];
    var password = userInfo.Length > 1 ? Uri.UnescapeDataString(userInfo[1]) : string.Empty;
    var host = uri.Host;
    var port = uri.Port > 0 ? uri.Port : 5432;
    var database = uri.AbsolutePath.TrimStart('/');
    var query = System.Web.HttpUtility.ParseQueryString(uri.Query);
    var sslMode = query["sslmode"] ?? "Prefer";
    var npgsqlSsl = sslMode.ToLowerInvariant() switch {
        "require"     => "Require",
        "verify-ca"   => "VerifyCA",
        "verify-full" => "VerifyFull",
        "disable"     => "Disable",
        _             => "Prefer"
    };
    // Only trust-cert for Require/Prefer — VerifyCA/VerifyFull must validate the chain
    var trustCert = npgsqlSsl is "Require" or "Prefer" ? ";Trust Server Certificate=true" : string.Empty;
    return $"Host={host};Port={port};Username={username};Password={password};Database={database};SSL Mode={npgsqlSsl}{trustCert}";
}
builder.Services.AddOptions();
builder.Services.AddControllersWithViews();

// API
builder.Services.AddControllers(); // or Minimal APIs
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
builder.Services.AddHttpClient<IEspnCoreOddsService, EspnCoreOddsService>(x => {
    x.BaseAddress = new Uri("https://sports.core.api.espn.com");
});
builder.Services.AddHttpClient<IEspnApiService, EspnApiService>(x => {
    x.BaseAddress = new Uri("http://site.api.espn.com");
});
if (builder.Configuration["DEMO_MODE"] == "true")
{
    builder.Services.AddSingleton<IEspnCacheService, DemoEspnCacheService>();
    builder.Services.AddScoped<DemoDataSeeder>();
}
else
    builder.Services.AddSingleton<IEspnCacheService, EspnCacheService>();
// Add HttpClient for Gridiron Uniforms and register Jersey cache service
builder.Services.AddHttpClient<IJerseyCacheService, JerseyCacheService>(c => {
    c.BaseAddress = new Uri("https://www.gridiron-uniforms.com/GUD/");
    // Some servers dislike aggressive headers; set a reasonable user agent
    c.DefaultRequestHeaders.UserAgent.ParseAdd("FourPlayJerseyCache/1.0");
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
});


builder.Services.AddAuthorization();
builder.Services.AddMemoryCache();

// CORS — allow configured origins (comma-separated ALLOWED_ORIGINS env var)
var allowedOrigins = (Environment.GetEnvironmentVariable("ALLOWED_ORIGINS") ?? "")
    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        if (allowedOrigins.Length > 0)
            policy.WithOrigins(allowedOrigins).AllowAnyHeader().AllowAnyMethod().AllowCredentials();
        else
            policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod();
    });
});
// Add Invitation Service
builder.Services.AddScoped<IInvitationService, InvitationService>();

builder.Services.AddScoped<ISpreadCalculatorBuilder, SpreadCalculatorBuilder>();
builder.Services.AddSingleton<ILeaderboardService, LeaderboardService>();
builder.Services.AddSingleton<ILeagueRepository, LeagueRepository>();
// Register job observer for observability
builder.Services.AddSingleton<IJobObserverService, JobObserverService>();

#region Quartz
// Quartz
builder.Services.AddScoped<IJob, NflScoresJob>();
builder.Services.AddScoped<IJob, NflSpreadJob>();
builder.Services.AddScoped<IJob, StartupJob>();
builder.Services.AddScoped<IJob, UserManagerJob>();
// Register MissingPicksJob
builder.Services.AddScoped<IJob, MissingPicksJob>();
builder.Services.AddQuartz(q => {
    q.UsePersistentStore(s => {
        // Use for Postgres database
        s.UsePostgres(postGresOptions => {
            postGresOptions.UseDriverDelegate<PostgreSQLDelegate>();
            postGresOptions.ConnectionString = connectionString;
            postGresOptions.TablePrefix = "quartz.qrtz_";
        });
        s.PerformSchemaValidation = true; // default
        s.UseProperties = true; // preferred, but not default
        s.RetryInterval = TimeSpan.FromSeconds(15);
        s.UseNewtonsoftJsonSerializer();
    });
    // Setup User at startup
 // User Manager
q.ScheduleJob<UserManagerJob>(trigger => trigger
    .WithIdentity("User Manager")
    .WithDescription("Manages initial user admin (mark)")
    .StartAt(DateBuilder.FutureDate(2, IntervalUnit.Minute))
);

// Note: use IANA timezone id "America/Chicago" which works on Linux containers
// NFL Spreads - special holiday
q.ScheduleJob<NflSpreadJob>(trigger => trigger
    .WithIdentity("NFL Spreads Christmas Eve")
    .WithDescription("Loads NFL spreads for Christmas Eve games at 10am CST")
    .WithCronSchedule("0 0 10 24 12 ?",
        x => x.WithMisfireHandlingInstructionFireAndProceed()
              .InTimeZone(TimeZoneInfo.FindSystemTimeZoneById("America/Chicago"))));
// NFL Spreads - weekly Thursday
q.ScheduleJob<NflSpreadJob>(trigger => trigger
    .WithIdentity("NFL Spreads Thursday 2pm")
    .WithDescription("Loads NFL spreads every Thursday at 2pm CST")
    .WithCronSchedule("0 0 14 ? * THU",
        x => x.WithMisfireHandlingInstructionFireAndProceed()
              .InTimeZone(TimeZoneInfo.FindSystemTimeZoneById("America/Chicago"))));
// NFL Scores - Thursday morning
q.ScheduleJob<NflScoresJob>(trigger => trigger
    .WithIdentity("NFL Scores Thu 10am")
    .WithDescription("Fetches NFL scores Thursday morning at 10am CST")
    .WithCronSchedule("0 0 10 ? * THU",
        x => x.WithMisfireHandlingInstructionFireAndProceed()
              .InTimeZone(TimeZoneInfo.FindSystemTimeZoneById("America/Chicago"))));
// NFL Scores - Friday overnight
q.ScheduleJob<NflScoresJob>(trigger => trigger
    .WithIdentity("NFL Scores Fri 1am")
    .WithDescription("Fetches NFL scores early Friday at 1am CST")
    .WithCronSchedule("0 0 1 ? * FRI",
        x => x.WithMisfireHandlingInstructionFireAndProceed()
              .InTimeZone(TimeZoneInfo.FindSystemTimeZoneById("America/Chicago"))));
// NFL Scores - Sunday pregame (Euro Games)
q.ScheduleJob<NflScoresJob>(trigger => trigger
    .WithIdentity("NFL Scores Sun 12:30pm")
    .WithDescription("Fetches NFL scores just before Sunday early games at 12:30pm CST")
    .WithCronSchedule("0 30 12 ? * SUN",
        x => x.WithMisfireHandlingInstructionFireAndProceed()
              .InTimeZone(TimeZoneInfo.FindSystemTimeZoneById("America/Chicago"))));
// NFL Scores - Sunday late afternoon
q.ScheduleJob<NflScoresJob>(trigger => trigger
    .WithIdentity("NFL Scores Sun 4:30pm")
    .WithDescription("Fetches NFL scores during Sunday late games at 4:30pm CST")
    .WithCronSchedule("0 30 16 ? * SUN",
        x => x.WithMisfireHandlingInstructionFireAndProceed()
              .InTimeZone(TimeZoneInfo.FindSystemTimeZoneById("America/Chicago"))));
// NFL Scores - Sunday evening afternoon
q.ScheduleJob<NflScoresJob>(trigger => trigger
    .WithIdentity("NFL Scores Sun 7:40pm")
    .WithDescription("Fetches NFL scores during Sunday late games at 7:40pm CST")
    .WithCronSchedule("0 40 19 ? * SUN",
        x => x.WithMisfireHandlingInstructionFireAndProceed()
            .InTimeZone(TimeZoneInfo.FindSystemTimeZoneById("America/Chicago"))));
// NFL Scores - Monday overnight
q.ScheduleJob<NflScoresJob>(trigger => trigger
    .WithIdentity("NFL Scores Mon 1am")
    .WithDescription("Fetches NFL scores early Monday after SNF at 1am CST")
    .WithCronSchedule("0 0 1 ? * MON",
        x => x.WithMisfireHandlingInstructionFireAndProceed()
              .InTimeZone(TimeZoneInfo.FindSystemTimeZoneById("America/Chicago"))));
// NFL Scores - Tues overnight
q.ScheduleJob<NflScoresJob>(trigger => trigger
    .WithIdentity("NFL Scores Tue 1am")
    .WithDescription("Fetches NFL scores early Monday after SNF at 1am CST")
    .WithCronSchedule("0 0 1 ? * TUE",
        x => x.WithMisfireHandlingInstructionFireAndProceed()
            .InTimeZone(TimeZoneInfo.FindSystemTimeZoneById("America/Chicago"))));
// Missing Picks Job - Sundays at 11:00 AM CST
// MissingPicksJob trigger disabled — frizat-z5h deferred.
// Plan: fire at 2:45pm CST Sat+Sun, gate send with HasGamesTodayAsync, replace hardcoded "noon CST" copy.
// q.ScheduleJob<MissingPicksJob>(trigger => trigger
//     .WithIdentity("Missing Picks Job")
//     .WithDescription("Sends reminder emails to users missing required picks")
//     .WithCronSchedule("0 0 11 ? * SUN",
//         x => x.WithMisfireHandlingInstructionFireAndProceed()
//               .InTimeZone(TimeZoneInfo.FindSystemTimeZoneById("America/Chicago"))));
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
    if (pending.Count > 0)
        Log.Information("Applying {Count} pending migration(s): {Names}", pending.Count, pending);
    else
        Log.Information("Database is up to date, no migrations needed");
    db.Database.Migrate();
}
catch (Exception ex)
{
    Log.Error(ex, "Error Upgrading DB");
    throw;
}

if (app.Configuration["DEMO_MODE"] == "true")
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
