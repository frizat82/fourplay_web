using System.Net;
using System.Text;
using FourPlayWebApp.Server.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace FourPlayWebApp.Server.UnitTests;

/// <summary>
/// frizat: an unattended cron (LeagueJuiceReminderJob) was emailing a real inbox from the demo
/// league seeded on dev, and separately /full-test-local's real-mode local runs send real email
/// too — neither is desired. GoogleEmailSender now suppresses real sends in any non-production
/// context (local dev via IWebHostEnvironment.IsDevelopment(), or the deployed Railway dev
/// environment via RAILWAY_ENVIRONMENT_NAME, since that deployment doesn't set
/// ASPNETCORE_ENVIRONMENT=Development and would otherwise report IsDevelopment()=false) and logs
/// what would have been sent instead of calling the Gmail API.
/// </summary>
public class GoogleEmailSenderTests
{
    private sealed class CapturingHandler : HttpMessageHandler
    {
        public int CallCount { get; private set; }
        public string? LastRequestUri { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            CallCount++;
            LastRequestUri = request.RequestUri?.ToString();
            // Token endpoint response shape — good enough for both the token call and (if ever
            // reached in a real-send test) is never hit since tests below don't set full creds.
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"access_token\":\"fake-token\"}"),
            });
        }
    }

    private static (GoogleEmailSender sut, CapturingHandler handler) Build(
        bool isDevelopment, string? railwayEnvironmentName, bool withCredentials = true)
    {
        Environment.SetEnvironmentVariable("FOURPLAY_EMAIL_USER", withCredentials ? "from@example.com" : null);
        Environment.SetEnvironmentVariable("GOOGLE_CLIENT_ID", withCredentials ? "client-id" : null);
        Environment.SetEnvironmentVariable("GOOGLE_CLIENT_SECRET", withCredentials ? "client-secret" : null);
        Environment.SetEnvironmentVariable("GOOGLE_REFRESH_TOKEN", withCredentials ? "refresh-token" : null);
        Environment.SetEnvironmentVariable("RAILWAY_ENVIRONMENT_NAME", railwayEnvironmentName);

        var handler = new CapturingHandler();
        var httpClient = new HttpClient(handler);
        var httpClientFactory = Substitute.For<IHttpClientFactory>();
        httpClientFactory.CreateClient().Returns(httpClient);

        var environment = Substitute.For<IWebHostEnvironment>();
        environment.EnvironmentName.Returns(isDevelopment ? "Development" : "Production");

        var sut = new GoogleEmailSender(NullLogger<GoogleEmailSender>.Instance, httpClientFactory, environment);
        return (sut, handler);
    }

    [Fact]
    public async Task SendEmailAsync_LocalDevelopment_DoesNotCallGmailApi()
    {
        var (sut, handler) = Build(isDevelopment: true, railwayEnvironmentName: null);

        await sut.SendEmailAsync("someone@example.com", "Test subject", "<p>body</p>");

        Assert.Equal(0, handler.CallCount);
    }

    [Fact]
    public async Task SendEmailAsync_DeployedRailwayDevelopmentEnvironment_DoesNotCallGmailApi()
    {
        // Simulates the deployed dev Railway environment: ASPNETCORE_ENVIRONMENT is unset there
        // (so IsDevelopment() is false, defaulting to "Production" hosting env), but Railway's
        // own RAILWAY_ENVIRONMENT_NAME is "development" — that's the signal this must catch too.
        var (sut, handler) = Build(isDevelopment: false, railwayEnvironmentName: "development");

        await sut.SendEmailAsync("someone@example.com", "Test subject", "<p>body</p>");

        Assert.Equal(0, handler.CallCount);
    }

    // /code-review: reproduces the exact regression an earlier version of this fix had. This
    // repo's own documented local-run command (`dotnet run --no-launch-profile ...`, CLAUDE.md
    // and start-demo.sh) skips launchSettings.json, so ASPNETCORE_ENVIRONMENT is never set on an
    // ordinary local run — IsDevelopment() defaults to false. With no Railway context either
    // (RAILWAY_ENVIRONMENT_NAME unset, since this isn't deployed), a check that only looked for
    // IsDevelopment()==true or RAILWAY_ENVIRONMENT_NAME=="development" would send real email here.
    [Fact]
    public async Task SendEmailAsync_LocalRunWithNoAspNetCoreEnvironmentSet_StillDoesNotCallGmailApi()
    {
        var (sut, handler) = Build(isDevelopment: false, railwayEnvironmentName: null);

        await sut.SendEmailAsync("someone@example.com", "Test subject", "<p>body</p>");

        Assert.Equal(0, handler.CallCount);
    }

    [Fact]
    public async Task SendEmailAsync_Production_SendsRealEmail()
    {
        var (sut, handler) = Build(isDevelopment: false, railwayEnvironmentName: "production");

        await sut.SendEmailAsync("someone@example.com", "Test subject", "<p>body</p>");

        // Token fetch + Gmail send = 2 HTTP calls when actually sending.
        Assert.Equal(2, handler.CallCount);
    }

    [Fact]
    public async Task SendEmailAsync_Production_EncodesNonAsciiSubjectAsRfc2047()
    {
        var (sut, handler) = Build(isDevelopment: false, railwayEnvironmentName: "production");

        // Capture the actual MIME payload sent to the Gmail API.
        string? capturedRaw = null;
        var handler2 = new CapturingBodyHandler();
        var httpClientFactory = Substitute.For<IHttpClientFactory>();
        httpClientFactory.CreateClient().Returns(new HttpClient(handler2));
        var environment = Substitute.For<IWebHostEnvironment>();
        environment.EnvironmentName.Returns("Production");
        var sutWithCapture = new GoogleEmailSender(NullLogger<GoogleEmailSender>.Instance, httpClientFactory, environment);

        await sutWithCapture.SendEmailAsync("someone@example.com", "Set up Juice for CFB Demo League — 2026 season", "<p>body</p>");

        capturedRaw = handler2.LastBody;
        Assert.NotNull(capturedRaw);
        // A raw, un-encoded em-dash must never appear directly in the MIME headers — RFC 2822
        // headers are 7-bit ASCII only. The subject must instead carry an RFC 2047 encoded-word.
        Assert.Contains("=?UTF-8?B?", capturedRaw);
    }

    private sealed class CapturingBodyHandler : HttpMessageHandler
    {
        public string? LastBody { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (request.RequestUri!.ToString().Contains("oauth2"))
            {
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("{\"access_token\":\"fake-token\"}"),
                };
            }

            var body = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
            // The Gmail API payload is {"raw": "<base64url of the MIME message>"} — decode it
            // back out so the assertion can inspect the actual header bytes.
            if (body is not null)
            {
                using var doc = System.Text.Json.JsonDocument.Parse(body);
                var raw = doc.RootElement.GetProperty("raw").GetString()!;
                var padded = raw.Replace('-', '+').Replace('_', '/');
                padded += new string('=', (4 - padded.Length % 4) % 4);
                LastBody = Encoding.UTF8.GetString(Convert.FromBase64String(padded));
            }

            return new HttpResponseMessage(HttpStatusCode.OK);
        }
    }
}
