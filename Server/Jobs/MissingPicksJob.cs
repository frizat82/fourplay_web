using Microsoft.AspNetCore.Identity.UI.Services;
using Quartz;
using Serilog;
using FourPlayWebApp.Shared.Helpers;
using FourPlayWebApp.Server.Services.Interfaces;
using FourPlayWebApp.Server.Services;
using FourPlayWebApp.Server.Services.Repositories.Interfaces;
using FourPlayWebApp.Shared.Helpers.Extensions; // for GoogleEmailSender cast

namespace FourPlayWebApp.Server.Jobs;

[DisallowConcurrentExecution]
public class MissingPicksJob(ILeagueRepository leagueRepository, IEspnApiService espiApiService, IEmailSender emailSender, IJobObserverService observer)
    : IJob {
    private readonly string _baseUrl = Environment.GetEnvironmentVariable("APP_URL") ?? throw new MissingFieldException("APP_URL Required");
    public async Task Execute(IJobExecutionContext context)
    {
        var jobName = nameof(MissingPicksJob);
        await observer.RecordJobStartAsync(jobName);
        try
        {
            Log.Information("MissingPicksJob started at {Time}", DateTimeOffset.UtcNow);

            var nowUtc = DateTimeOffset.UtcNow;
            // Attempt to find the current NFL week from the stored weeks
            var scoreboard = await espiApiService.GetScores();
            if (scoreboard is null) {
                Log.Warning("MissingPicksJob: Unable to retrieve current NFL week from ESPN");
                await observer.RecordJobFailureAsync(jobName, "Unable to retrieve current NFL week from ESPN");
                return;
            }
            var season = scoreboard.Season.Year;
            var currentWeek = scoreboard.Week;
            if (currentWeek == null)
            {
                Log.Warning("MissingPicksJob: Unable to determine current or upcoming NFL week for season {Season}", season);
                await observer.RecordJobFailureAsync(jobName, "Unable to determine current or upcoming NFL week");
                return;
            }

            var weekNumber = currentWeek.Number;
            var isPostSeason = scoreboard.IsPostSeason();
            var actualWeek = GameHelpers.GetWeekFromEspnWeek(weekNumber, isPostSeason);
            var requiredPicks = GameHelpers.GetEspnRequiredPicks(weekNumber, isPostSeason);
            Log.Information("MissingPicksJob: season={Season} week={Week} isPostSeason={IsPost} requiredPicks={Required}", season, weekNumber, isPostSeason, requiredPicks);

            // Get all users and iterate their league memberships
            var users = await leagueRepository.GetUsersAsync();
            if (users.Count == 0)
            {
                Log.Information("MissingPicksJob: No users found to check.");
                await observer.RecordJobSuccessAsync(jobName, "No users to process");
                return;
            }

            foreach (var user in users)
            {
                try
                {
                    // Aggregate missing leagues for this user so we send one email per user
                    var missingLeagues = new List<(int LeagueId, string LeagueName, int PicksCount, int Required, int Missing)>();

                    // Get leagues this user is a member of
                    var mappings = await leagueRepository.GetLeagueUserMappingsAsync(user);
                    if (mappings.Count == 0) continue;

                    foreach (var mapping in mappings)
                    {
                        var leagueId = mapping.LeagueId;

                        // Get user's picks for this league/week
                        var userPicks = await leagueRepository.GetUserNflPicksAsync(user.Id, leagueId, (int)season, (int)weekNumber);
                        var picksCount = userPicks.Count;

                        if (picksCount >= requiredPicks) continue;
                        var missing = requiredPicks - picksCount;
                        var league = mapping.League;
                        var leagueName = league is not null ? league.LeagueName : leagueId.ToString();

                        missingLeagues.Add((LeagueId: leagueId, LeagueName: leagueName, PicksCount: picksCount, Required: requiredPicks, Missing: missing));
                    }

                    if (missingLeagues.Count <= 0)
                        continue;
                    if (string.IsNullOrWhiteSpace(user.Email))
                    {
                        Log.Warning("MissingPicksJob: User {UserId} has no email address configured; skipping email", user.Id);
                    }
                    else
                    {
                        // Capture checked email into a local non-null variable to satisfy nullable analysis
                        var email = user.Email!;
                        // Build a single email listing all leagues the user is missing picks for
                        var subject = $"Reminder: Missing picks for Week {actualWeek}";

                        var sb = new System.Text.StringBuilder();
                        sb.Append($"<p>Hello <strong>{System.Net.WebUtility.HtmlEncode(user.UserName)}</strong>,</p>");
                        sb.Append($"<p>This is a friendly reminder that you are missing picks for <strong>Week {actualWeek}</strong>. Please submit your picks for the following league(s):</p>");
                        sb.Append("<ul>");
                        foreach (var ml in missingLeagues)
                        {
                            sb.Append($"<li><strong>{System.Net.WebUtility.HtmlEncode(ml.LeagueName)}</strong> — You have <strong>{ml.PicksCount}</strong> of the required <strong>{ml.Required}</strong> picks (<strong>{ml.Missing}</strong> missing)</li>");
                        }
                        sb.Append("</ul>");
                        sb.Append($"<p>Please submit your picks before <strong>Noon CST</strong> today to avoid forfeiting the week.</p>");
                        sb.Append("<p style='text-align:center;margin:18px 0;'><a href='" + _baseUrl + "' style='display:inline-block;background-color:#4f46e5;color:#fff;text-decoration:none;padding:10px 18px;border-radius:6px;'>Make Picks Now</a></p>");
                        sb.Append("<p>If you believe this is an error, please contact your league administrator.</p>");

                        var messageBody = sb.ToString();

                        try {
                            var templated = GoogleEmailSender.CreateTemplatedBody(subject, messageBody); 
                            await emailSender.SendEmailAsync(email, subject, templated);
                            Log.Information("MissingPicksJob: Sent aggregated reminder to {Email} for user {UserId}", user.Email, user.Id);
                        }
                        catch (Exception exEmail) {
                            Log.Error(exEmail, "MissingPicksJob: Failed to send aggregated email to {Email}", user.Email);
                        }
                    }
                }
                catch (Exception exUser)
                {
                    Log.Error(exUser, "MissingPicksJob: failed processing user {UserId}", user?.Id);
                    // record per-user failure but continue processing others
                }
            }

            Log.Information("MissingPicksJob finished at {Time}", DateTimeOffset.UtcNow);
            await observer.RecordJobSuccessAsync(jobName, "Completed successfully");
        }
        catch (Exception ex)
        {
            Log.Error(ex, "MissingPicksJob failed");
            await observer.RecordJobFailureAsync(jobName, ex.Message);
            throw;
        }
    }
}
