using System.Net;

namespace FourPlayWebApp.Server.UnitTests.TestHelpers;

// Shared by CfbApiServiceTests and ESPNApiServiceTests (frizat-4k9) — both faked ESPN's HTTP
// response the same way (capture the request, return a settable body/status) as two independent
// copies before this was pulled out. One fake, reused per sport's API service test file.
public sealed class CapturingHandler : HttpMessageHandler {
    public Uri? LastRequestUri { get; private set; }
    // frizat-4gn: a date window is fetched one day at a time (see EspnDateRangeFetcher) — tests
    // asserting how many/which requests went out need every URI, not just the last.
    public List<Uri> RequestUris { get; } = [];
    public string ResponseBody { get; set; } = "{}";
    public HttpStatusCode StatusCode { get; set; } = HttpStatusCode.OK;

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) {
        LastRequestUri = request.RequestUri;
        RequestUris.Add(request.RequestUri!);
        return Task.FromResult(new HttpResponseMessage(StatusCode) {
            Content = new StringContent(ResponseBody),
        });
    }
}
