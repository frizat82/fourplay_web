using System.Net;

namespace FourPlayWebApp.Server.UnitTests.TestHelpers;

// Shared by CfbApiServiceTests and ESPNApiServiceTests (frizat-4k9) — both faked ESPN's HTTP
// response the same way (capture the request, return a settable body/status) as two independent
// copies before this was pulled out. One fake, reused per sport's API service test file.
public sealed class CapturingHandler : HttpMessageHandler {
    public Uri? LastRequestUri { get; private set; }
    public string ResponseBody { get; set; } = "{}";
    public HttpStatusCode StatusCode { get; set; } = HttpStatusCode.OK;

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) {
        LastRequestUri = request.RequestUri;
        return Task.FromResult(new HttpResponseMessage(StatusCode) {
            Content = new StringContent(ResponseBody),
        });
    }
}
