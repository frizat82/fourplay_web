using FourPlayWebApp.Server.Models.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.Extensions.Logging;
using System.Net;
using System.Net.Mail;
using System.Text;

namespace FourPlayWebApp.Server.Services;

public class GoogleEmailSender(ILogger<GoogleEmailSender> logger) : IEmailSender<ApplicationUser>, IEmailSender {
    private const string _smtpHost = "smtp.gmail.com";
    private const int _smtpPort = 587;
    // Require environment variables with no hardcoded fallbacks
    private readonly string? _userName = Environment.GetEnvironmentVariable("FOURPLAY_EMAIL_USER");
    private readonly string? _password = Environment.GetEnvironmentVariable("FOURPLAY_EMAIL_PASS");

    #region Public Email Sender Methods

    public async Task SendEmailAsync(string toEmail, string subject, string htmlBody) {
        // Guard: fail fast and log if credentials aren't configured
        if (string.IsNullOrWhiteSpace(_userName) || string.IsNullOrWhiteSpace(_password)) {
            logger.LogError("Email credentials are not configured. Please set FOURPLAY_EMAIL_USER and FOURPLAY_EMAIL_PASS environment variables.");
            return; // don't throw here; startup validation will enforce presence earlier
        }

        try {
            using var message = new MailMessage {
                From = new MailAddress(_userName, "FourPlay"),
                Subject = subject,
                Body = htmlBody,
                IsBodyHtml = true
            };

            message.To.Add(toEmail);

            using var smtpClient = new SmtpClient(_smtpHost, _smtpPort) {
                Credentials = new NetworkCredential(_userName, _password),
                EnableSsl = true
            };

            await smtpClient.SendMailAsync(message);
            logger.LogInformation("\u2705 Email sent to {Email} ({Subject})", toEmail, subject);
        }
        catch (Exception ex) {
            logger.LogError(ex, "\u274c Failed to send email to {Email} ({Subject})", toEmail, subject);
        }
    }

    public Task SendConfirmationLinkAsync(ApplicationUser user, string email, string confirmationLink) {
        var body = BuildEmailTemplate(
            title: "Confirm Your Email",
            message: $"""
                      <p>Hello <strong>{user.UserName}</strong>,</p>
                      <p>Thanks for signing up! Please confirm your email address by clicking the button below.</p>
                      <div style="text-align:center;margin:24px 0;">
                        <a href="{confirmationLink}" style="display:inline-block;background-color:#4f46e5;color:#fff;text-decoration:none;padding:14px 30px;border-radius:6px;font-weight:bold;">
                          Confirm Email
                        </a>
                      </div>
                      <p>If you didn\u2019t create an account, you can safely ignore this message.</p>
                      """);

        return SendEmailAsync(email, "Confirm your FourPlay email", body);
    }

    public Task SendPasswordResetLinkAsync(ApplicationUser user, string email, string resetLink) {
        var body = BuildEmailTemplate(
            title: "Reset Your Password",
            message: $"""
                      <p>Hello <strong>{user.UserName}</strong>,</p>
                      <p>You recently requested to reset your password.</p>
                      <p>Click the button below to create a new password:</p>
                      <div style="text-align:center;margin:24px 0;">
                        <a href="{resetLink}" style="display:inline-block;background-color:#4f46e5;color:#ffffff;text-decoration:none;padding:14px 30px;border-radius:6px;font-weight:bold;">
                          Reset Password
                        </a>
                      </div>
                      <p>If you didn’t request this change, you can safely ignore this email.</p>
                      """);

        return SendEmailAsync(email, "Reset your FourPlay password", body);
    }

    public async Task SendPasswordResetCodeAsync(ApplicationUser user, string email, string resetCode) {
        var body = BuildEmailTemplate(
            title: "Your Password Reset Code",
            message: $"""
                      <p>Hello <strong>{user.UserName}</strong>,</p>
                      <p>You recently requested to reset your password.</p>
                      <p>Use the following code to complete your password reset:</p>
                      <div style="text-align:center;margin:30px 0;">
                        <div style="display:inline-block;background-color:#4f46e5;color:#ffffff;font-size:24px;font-weight:bold;letter-spacing:3px;padding:14px 28px;border-radius:6px;">
                          {WebUtility.HtmlEncode(resetCode)}
                        </div>
                      </div>
                      <p>If you didn’t request this change, you can safely ignore this email.</p>
                      """);

        await SendEmailAsync(email, "Your FourPlay password reset code", body);
    }

    /// <summary>
    /// Send a simple templated notification using the FourPlay template.
    /// Accepts an optional ApplicationUser to personalize the message, but is not required.
    /// </summary>
    public Task SendTemplatedMessageAsync(ApplicationUser? user, string email, string title, string message) {
        // If we have a user, personalize the message with their username where applicable
        var personalizedMessage = user is null ? message : $"<p>Hello <strong>{WebUtility.HtmlEncode(user.UserName)}</strong>,</p>\n" + message;
        var body = BuildEmailTemplate(title: title, message: personalizedMessage);
        return SendEmailAsync(email, title, body);
    }

    #endregion

    #region Private Template Helpers

    /// <summary>
    /// Builds a unified FourPlay email layout with consistent design.
    /// </summary>
    private static string BuildEmailTemplate(string title, string message) {
        var sb = new StringBuilder();
        sb.Append($"""
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
                      FourPlay
                    </td>
                  </tr>
                  <tr>
                    <td style="padding:36px 44px;color:#333333;font-size:15px;line-height:1.6;">
                      {message}
                      <p style="margin-top:32px;font-size:13px;color:#777;text-align:center;">Thanks,<br/>The FourPlay Team</p>
                    </td>
                  </tr>
                  <tr>
                    <td style="background-color:#f2f2f2;padding:14px;text-align:center;font-size:12px;color:#888888;">
                      &copy; {DateTime.UtcNow.Year} FourPlay. All rights reserved.
                    </td>
                  </tr>
                </table>
              </td>
            </tr>
          </table>
        </body>
        </html>
        """);

        return sb.ToString();
    }

    /// <summary>
    /// Public wrapper for external callers that want the FourPlay HTML-wrapped body.
    /// </summary>
    public static string CreateTemplatedBody(string title, string message) => BuildEmailTemplate(title, message);

    #endregion
}
