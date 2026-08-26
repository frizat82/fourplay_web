using FourPlayWebApp.Server.Models.Identity;
using FourPlayWebApp.Server.Services;
using FourPlayWebApp.Server.Services.Interfaces;
using FourPlayWebApp.Server.Services.Repositories.Interfaces;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Quartz;

namespace FourPlayWebApp.Server.Jobs;

// frizat-ugs: fires once per (league, season), scheduled by LeagueJuiceSchedulerJob at
// lockTime - 2 days. Emails the league owner if Juice is still unconfigured for that season.
[DisallowConcurrentExecution]
public class LeagueJuiceReminderJob(
    ILeagueRepository repo,
    UserManager<ApplicationUser> userManager,
    IEmailSender emailSender,
    IJobObserverService observer) : IJob {

    public async Task Execute(IJobExecutionContext context) {
        var jobName = nameof(LeagueJuiceReminderJob);
        await observer.RecordJobStartAsync(jobName);
        try {
            // /code-review: reading this in a field initializer would throw during job
            // construction — before this try block — bypassing RecordJobFailureAsync entirely and
            // making a missing APP_URL invisible to job-health monitoring.
            var baseUrl = Environment.GetEnvironmentVariable("APP_URL") ?? throw new MissingFieldException("APP_URL Required");
            var (leagueId, season) = LeagueJuiceJobData.Parse(context);

            // Re-check freshness at fire time — the scheduler's snapshot may be stale by now;
            // the owner may have configured Juice in the days since this trigger was registered.
            if (await repo.GetLeagueJuiceMappingAsync(leagueId, season) is not null) {
                await observer.RecordJobSuccessAsync(jobName, $"League {leagueId} season {season} already configured — no email sent");
                return;
            }
            // "Juice configured" isn't a valid "already reminded" signal (sending this email
            // doesn't configure anything) — this persisted marker is the real one, checked again
            // here in case of a rare double-fire race, on top of LeagueJuiceScheduleSource's own
            // HasData check already excluding this candidate from future scheduler passes.
            if ((await repo.GetJuiceRemindersSentAsync()).Contains((leagueId, season))) {
                await observer.RecordJobSuccessAsync(jobName, $"League {leagueId} season {season} reminder already sent — skipping");
                return;
            }

            var league = await repo.GetLeagueInfoAsync(leagueId);
            var owner = await userManager.FindByIdAsync(league.OwnerUserId);
            // A missing owner email is a real misconfiguration an admin needs to act on — throw
            // (rather than log-and-return) so it reaches the catch block below and, from there,
            // the global JobFailureAlertListener/Discord alert.
            if (owner?.Email is null) {
                throw new InvalidOperationException($"League {leagueId} owner has no email on file");
            }

            var portalUrl = $"{baseUrl.TrimEnd('/')}/league/manage";
            // League names are free-text and fully user-controlled (unlike ApplicationUser
            // usernames, which ASP.NET Identity already restricts to a safe character set) —
            // HTML-encode before interpolating into the email body.
            var encodedLeagueName = System.Net.WebUtility.HtmlEncode(league.LeagueName);
            var body = GoogleEmailSender.CreateTemplatedBody(
                "Set your league's Juice before the season locks",
                $"""
                 <p>Hello,</p>
                 <p>The {season} season is about to start for <strong>{encodedLeagueName}</strong>, and Juice hasn't been configured yet.</p>
                 <p>If it's still unconfigured when the season locks, we'll automatically carry forward last season's amounts (or use the defaults, for a new league) — but you can set your own values now in the League Portal.</p>
                 <div style="text-align:center;margin:24px 0;">
                   <a href="{portalUrl}" style="display:inline-block;background-color:#4f46e5;color:#fff;text-decoration:none;padding:14px 30px;border-radius:6px;font-weight:bold;">
                     Configure Juice
                   </a>
                 </div>
                 """);
            await emailSender.SendEmailAsync(owner.Email, $"Set up Juice for {league.LeagueName} — {season} season", body);
            // /code-review: a DB failure here (after the email already went out) would leave this
            // reminder unrecorded, so the next catch-up pass could resend it once more — accepted
            // as a rare residual risk (there's no way to make "send an email" and "write to
            // Postgres" atomic) rather than adding retry/idempotency machinery for it; a failure
            // here still surfaces via the catch block below, which now rethrows so
            // frizat-703.2's global job-failure alert actually sees it.
            await repo.RecordJuiceReminderSentAsync(leagueId, season);
            await observer.RecordJobSuccessAsync(jobName, $"Reminder sent to league {leagueId} owner for season {season}");
        } catch (Exception ex) {
            await observer.RecordAndRethrowAsync(jobName, ex);
        }
    }
}
