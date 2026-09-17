using System.Net;

namespace FourPlayWebApp.Server.UnitTests.TestHelpers;

// Shared by CfbApiServiceTests and ESPNApiServiceTests (frizat-4k9) — both faked ESPN's HTTP
// response the same way (capture the request, return a settable body/status) as two independent
// copies before this was pulled out. One fake, reused per sport's API service test file.
public sealed class CapturingHandler : HttpMessageHandler {
    public Uri? LastRequestUri { get; private set; }
    // frizat-4gn: day-by-day date-range fetching (ESPN broke the dates=START-END range query —
    // see EspnDateRangeFetcher) makes one HTTP call per day in the window, not one call total —
    // tests asserting how many/which requests went out need every URI, not just the last.
    public List<Uri> RequestUris { get; } = [];
    public string ResponseBody { get; set; } = "{}";
    public HttpStatusCode StatusCode { get; set; } = HttpStatusCode.OK;
    // Optional per-call override queue, dequeued in call order — lets a test simulate "the first
    // call (the range attempt) fails, every call after that (day-by-day) succeeds." Falls back to
    // the plain ResponseBody/StatusCode above once empty.
    public Queue<(HttpStatusCode StatusCode, string Body)> ResponseQueue { get; } = new();

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) {
        LastRequestUri = request.RequestUri;
        RequestUris.Add(request.RequestUri!);
        var (statusCode, body) = ResponseQueue.Count > 0 ? ResponseQueue.Dequeue() : (StatusCode, ResponseBody);
        return Task.FromResult(new HttpResponseMessage(statusCode) {
            Content = new StringContent(body),
        });
    }
}
