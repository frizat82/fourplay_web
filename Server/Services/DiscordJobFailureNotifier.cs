using System.Collections.Concurrent;
using System.Net.Http.Json;
using FourPlayWebApp.Server.Services.Interfaces;

namespace FourPlayWebApp.Server.Services;

// frizat-703.2: one Discord message per job identity per 6h window — a job stuck
// retrying/misfiring shouldn't spam the channel, but a different job failing at the same time
// is independently actionable and still gets its own message. Dedup state is an in-process
// dictionary (not IMemoryCache) so the 6h window can be driven by the injected TimeProvider in
// tests without also needing to fake MemoryCache's own internal clock.
//
// JobFailureAlertListener (the only consumer) is registered as a Quartz DI singleton, so this
// notifier lives for the whole process too — it resolves an HttpClient from IHttpClientFactory
// per call rather than holding one via constructor injection, avoiding the "typed client
// captured by a singleton" pattern that would pin a single SocketsHttpHandler (and its cached
// DNS resolution) for the app's entire lifetime.
public class DiscordJobFailureNotifier(
    IHttpClientFactory httpClientFactory,
    TimeProvider timeProvider,
    IConfiguration configuration,
    ILogger<DiscordJobFailureNotifier> logger) : IJobFailureNotifier
{
    internal const string HttpClientName = nameof(DiscordJobFailureNotifier);

    private static readonly TimeSpan DedupeWindow = TimeSpan.FromHours(6);
    private readonly ConcurrentDictionary<string, DateTimeOffset> _lastAlerted = new();

    public async Task NotifyAsync(string jobName, string triggerName, string errorMessage, CancellationToken cancellationToken = default)
    {
        var webhookUrl = configuration["DISCORD_ALERT_WEBHOOK_URL"];
        if (string.IsNullOrWhiteSpace(webhookUrl))
        {
            logger.LogWarning("DISCORD_ALERT_WEBHOOK_URL not configured — job failure alert for {JobName} was not sent", jobName);
            return;
        }

        var now = timeProvider.GetUtcNow();
        if (_lastAlerted.TryGetValue(jobName, out var last) && now - last < DedupeWindow)
        {
            return;
        }

        var payload = new DiscordWebhookPayload(
            $"⚠️ **Job failed:** `{jobName}`\nTrigger: `{triggerName}`\nError: {errorMessage}");

        try
        {
            var httpClient = httpClientFactory.CreateClient(HttpClientName);
            var response = await httpClient.PostAsJsonAsync(webhookUrl, payload, cancellationToken);
            response.EnsureSuccessStatusCode();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to send Discord job failure alert for {JobName}", jobName);
            return; // don't record the alert time — let the next failure retry the send
        }

        _lastAlerted[jobName] = now;
    }

    private record DiscordWebhookPayload(string Content);
}
