using FourPlayWebApp.Server.Models.Identity;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.Extensions.Logging;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace FourPlayWebApp.Server.Services;

public class GoogleEmailSender(ILogger<GoogleEmailSender> logger, IHttpClientFactory httpClientFactory, IWebHostEnvironment environment)
    : IEmailSender<ApplicationUser>, IEmailSender {

    private readonly string? _fromEmail    = Environment.GetEnvironmentVariable("FOURPLAY_EMAIL_USER");
    private readonly string? _clientId     = Environment.GetEnvironmentVariable("GOOGLE_CLIENT_ID");
    private readonly string? _clientSecret = Environment.GetEnvironmentVariable("GOOGLE_CLIENT_SECRET");
    private readonly string? _refreshToken = Environment.GetEnvironmentVariable("GOOGLE_REFRESH_TOKEN");

    // frizat: an unattended cron (LeagueJuiceReminderJob) was emailing a real inbox from the demo
    // league seeded on dev, and local /full-test-local real-mode runs send real email too — the
    // owner decided neither is wanted.
    //
    // /code-review caught a real gap in an earlier version of this check: it suppressed only when
    // environment.IsDevelopment() was true OR RAILWAY_ENVIRONMENT_NAME=="development" — but this
    // repo's own documented local-run command (`dotnet run --no-launch-profile ...`, used in
    // CLAUDE.md/start-demo.sh) deliberately skips launchSettings.json, so ASPNETCORE_ENVIRONMENT
    // is never set locally and .NET defaults IsDevelopment() to false. That earlier version would
    // have sent real email on every ordinary local run — the exact incident this fix exists to
    // prevent. Fixed by inverting to a fail-safe allow-list instead of a fail-open deny-list: this
    // app is ONLY ever deployed via Railway (dev + prod, both in the same project/service — see
    // CLAUDE.md's Hosting section), so RAILWAY_ENVIRONMENT_NAME is always present when actually
    // deployed and always absent when running on a developer's own machine. Real email now
    // requires an explicit "production" value; every other case (missing entirely, "development",
    // or anything unexpected) suppresses. environment.IsDevelopment() is kept as an extra guard in
    // case RAILWAY_ENVIRONMENT_NAME is ever misconfigured while genuinely running locally.
    private bool IsProductionEnvironment =>
        string.Equals(Environment.GetEnvironmentVariable("RAILWAY_ENVIRONMENT_NAME"), "production", StringComparison.OrdinalIgnoreCase)
        && !environment.IsDevelopment();

    #region Public Email Sender Methods

    public async Task SendEmailAsync(string toEmail, string subject, string htmlBody) {
        if (!IsProductionEnvironment) {
            // Log the full body (not just recipient/subject) so /full-test-local's auth-lifecycle
            // checks stay executable without a real inbox — confirmation/reset links AND reset
            // codes (SendPasswordResetCodeAsync has no link to grep for) are both visible in the
            // raw HTML, so a tester copies whichever one they need straight out of the console.
            logger.LogInformation(
                "📧 Email suppressed (non-production environment): {Email} ({Subject})\n{Body}",
                toEmail, subject, htmlBody);
            return;
        }

        if (string.IsNullOrWhiteSpace(_fromEmail) || string.IsNullOrWhiteSpace(_clientId)
            || string.IsNullOrWhiteSpace(_clientSecret) || string.IsNullOrWhiteSpace(_refreshToken)) {
            logger.LogError("Gmail API credentials not configured. Set FOURPLAY_EMAIL_USER, GOOGLE_CLIENT_ID, GOOGLE_CLIENT_SECRET, GOOGLE_REFRESH_TOKEN.");
            return;
        }

        try {
            var accessToken = await GetAccessTokenAsync();
            await SendViaGmailApiAsync(toEmail, subject, htmlBody, accessToken);
            logger.LogInformation("✅ Email sent to {Email} ({Subject})", toEmail, subject);
        } catch (Exception ex) {
            logger.LogError(ex, "❌ Failed to send email to {Email} ({Subject})", toEmail, subject);
        }
    }

    public Task SendConfirmationLinkAsync(ApplicationUser user, string email, string confirmationLink) =>
        SendEmailAsync(email, "Confirm your IV League email", BuildEmailTemplate(
            "Confirm Your Email",
            $"""
             <p>Hello <strong>{user.UserName}</strong>,</p>
             <p>Thanks for signing up! Please confirm your email address by clicking the button below.</p>
             <div style="text-align:center;margin:24px 0;">
               <a href="{confirmationLink}" style="display:inline-block;background-color:#4f46e5;color:#fff;text-decoration:none;padding:14px 30px;border-radius:6px;font-weight:bold;">
                 Confirm Email
               </a>
             </div>
             <p>If you didn't create an account, you can safely ignore this message.</p>
             """));

    public Task SendPasswordResetLinkAsync(ApplicationUser user, string email, string resetLink) =>
        SendEmailAsync(email, "Reset your IV League password", BuildEmailTemplate(
            "Reset Your Password",
            $"""
             <p>Hello <strong>{user.UserName}</strong>,</p>
             <p>You recently requested to reset your password.</p>
             <p>Click the button below to create a new password:</p>
             <div style="text-align:center;margin:24px 0;">
               <a href="{resetLink}" style="display:inline-block;background-color:#4f46e5;color:#ffffff;text-decoration:none;padding:14px 30px;border-radius:6px;font-weight:bold;">
                 Reset Password
               </a>
             </div>
             <p>If you didn't request this change, you can safely ignore this email.</p>
             """));

    public async Task SendPasswordResetCodeAsync(ApplicationUser user, string email, string resetCode) =>
        await SendEmailAsync(email, "Your IV League password reset code", BuildEmailTemplate(
            "Your Password Reset Code",
            $"""
             <p>Hello <strong>{user.UserName}</strong>,</p>
             <p>You recently requested to reset your password.</p>
             <p>Use the following code to complete your password reset:</p>
             <div style="text-align:center;margin:30px 0;">
               <div style="display:inline-block;background-color:#4f46e5;color:#ffffff;font-size:24px;font-weight:bold;letter-spacing:3px;padding:14px 28px;border-radius:6px;">
                 {WebUtility.HtmlEncode(resetCode)}
               </div>
             </div>
             <p>If you didn't request this change, you can safely ignore this email.</p>
             """));

    public Task SendTemplatedMessageAsync(ApplicationUser? user, string email, string title, string message) {
        var personalizedMessage = user is null ? message
            : $"<p>Hello <strong>{WebUtility.HtmlEncode(user.UserName)}</strong>,</p>\n" + message;
        return SendEmailAsync(email, title, BuildEmailTemplate(title, personalizedMessage));
    }

    #endregion

    #region Gmail API

    private async Task<string> GetAccessTokenAsync() {
        var http = httpClientFactory.CreateClient();
        var resp = await http.PostAsync("https://oauth2.googleapis.com/token",
            new FormUrlEncodedContent(new Dictionary<string, string> {
                ["client_id"]     = _clientId!,
                ["client_secret"] = _clientSecret!,
                ["refresh_token"] = _refreshToken!,
                ["grant_type"]    = "refresh_token",
            }));
        resp.EnsureSuccessStatusCode();
        var json = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
        return json.RootElement.GetProperty("access_token").GetString()
            ?? throw new InvalidOperationException("No access_token in response");
    }

    private async Task SendViaGmailApiAsync(string toEmail, string subject, string htmlBody, string accessToken) {
        // RFC 2822 MIME message — Gmail API requires base64url encoding of the raw message
        // RFC 2822 headers are 7-bit ASCII only — a subject with any non-ASCII character (e.g.
        // an em-dash) sent raw here isn't a valid header and gets mis-decoded by mail clients as
        // mojibake ("Ã¢Â€Â"" for an em-dash). RFC 2047 encoded-word syntax is the correct fix;
        // the body doesn't need this since it's declared UTF-8 via Content-Type below.
        var encodedSubject = $"=?UTF-8?B?{Convert.ToBase64String(Encoding.UTF8.GetBytes(subject))}?=";

        var mime = new StringBuilder();
        mime.AppendLine($"From: IV League <{_fromEmail}>");
        mime.AppendLine($"To: {toEmail}");
        mime.AppendLine($"Subject: {encodedSubject}");
        mime.AppendLine("MIME-Version: 1.0");
        mime.AppendLine("Content-Type: text/html; charset=utf-8");
        mime.AppendLine();
        mime.Append(htmlBody);

        var raw = Convert.ToBase64String(Encoding.UTF8.GetBytes(mime.ToString()))
            .Replace('+', '-').Replace('/', '_').TrimEnd('='); // base64url

        var http = httpClientFactory.CreateClient();
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        var payload = JsonSerializer.Serialize(new { raw });
        var resp = await http.PostAsync(
            "https://gmail.googleapis.com/gmail/v1/users/me/messages/send",
            new StringContent(payload, Encoding.UTF8, "application/json"));
        resp.EnsureSuccessStatusCode();
    }

    #endregion

    #region Template

    private static string BuildEmailTemplate(string title, string message) => $"""
        <!DOCTYPE html>
        <html>
        <head>
          <meta charset="UTF-8" />
          <meta name="viewport" content="width=device-width, initial-scale=1.0" />
          <title>{WebUtility.HtmlEncode(title)}</title>
        </head>
        <body style="margin:0;padding:0;background-color:#f8f9fa;font-family:Arial,Helvetica,sans-serif;">
          <table width="100%" cellpadding="0" cellspacing="0" role="presentation" style="padding:40px 0;">
            <tr>
              <td align="center">
                <table width="520" cellpadding="0" cellspacing="0" style="background-color:#ffffff;border-radius:8px;overflow:hidden;box-shadow:0 4px 12px rgba(0,0,0,0.08);">
                  <tr>
                    <td style="background-color:#4f46e5;padding:18px;text-align:center;color:#ffffff;font-size:22px;font-weight:bold;">
                      IV League
                    </td>
                  </tr>
                  <tr>
                    <td style="padding:36px 44px;color:#333333;font-size:15px;line-height:1.6;">
                      {message}
                      <p style="margin-top:32px;font-size:13px;color:#777;text-align:center;">Thanks,<br/>The IV League Team</p>
                    </td>
                  </tr>
                  <tr>
                    <td style="background-color:#f2f2f2;padding:14px;text-align:center;font-size:12px;color:#888888;">
                      &copy; {DateTime.UtcNow.Year} IV League. All rights reserved.
                    </td>
                  </tr>
                </table>
              </td>
            </tr>
          </table>
        </body>
        </html>
        """;

    public static string CreateTemplatedBody(string title, string message) => BuildEmailTemplate(title, message);

    #endregion
}
