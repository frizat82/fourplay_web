using System.Text;
using FourPlayWebApp.Server.Infrastructure;
using Microsoft.AspNetCore.Http;

namespace FourPlayWebApp.Server.UnitTests;

public class SseHelperTests {
    // Heartbeats must be a real `data:` event, not a bare SSE comment — a comment line never
    // fires the browser's EventSource.onmessage, so the client-side watchdog in
    // useReconnectingEventSource.ts (which resets on every message, heartbeat included) would
    // never see it and would eventually force-reconnect a perfectly healthy connection.
    [Fact]
    public async Task StreamAsync_WritesHeartbeatAsDataEvent_NotBareComment() {
        var context = new DefaultHttpContext();
        var body = new MemoryStream();
        context.Response.Body = body;
        using var cts = new CancellationTokenSource();

        Action? onChanged = null;
        var streamTask = SseHelper.StreamAsync(
            context.Response,
            h => onChanged = h,
            _ => { },
            cts.Token,
            heartbeatInterval: TimeSpan.FromMilliseconds(15));

        // Let a couple of heartbeat ticks fire, then cancel to unblock the stream loop. Generous
        // margin over the interval to stay robust on a loaded CI runner.
        await Task.Delay(150);
        cts.Cancel();
        await streamTask;

        var written = Encoding.UTF8.GetString(body.ToArray());
        Assert.Contains(SseHelper.HeartbeatMessage, written);
        // The old bare-comment format was a line starting with ": heartbeat" with nothing before
        // the colon — distinct from "data: heartbeat", which legitimately contains that same
        // substring. Checking no line *starts* with it is what actually proves the comment-only
        // form is gone.
        Assert.DoesNotContain("\n: heartbeat", "\n" + written);
    }

    [Fact]
    public async Task StreamAsync_WritesScoresUpdatedMessage_WhenSubscribedHandlerFires() {
        var context = new DefaultHttpContext();
        var body = new MemoryStream();
        context.Response.Body = body;
        using var cts = new CancellationTokenSource();

        Action? onChanged = null;
        var streamTask = SseHelper.StreamAsync(
            context.Response,
            h => onChanged = h,
            _ => { },
            cts.Token,
            // Long enough that no heartbeat fires during this test — isolates the assertion to
            // the subscribed-handler path.
            heartbeatInterval: TimeSpan.FromMinutes(5));

        onChanged?.Invoke();
        await Task.Delay(20);
        cts.Cancel();
        await streamTask;

        var written = Encoding.UTF8.GetString(body.ToArray());
        Assert.Contains(SseHelper.ScoresUpdatedMessage, written);
    }

    [Fact]
    public async Task StreamAsync_UnsubscribesOnCancellation() {
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();
        using var cts = new CancellationTokenSource();

        var unsubscribed = false;
        var streamTask = SseHelper.StreamAsync(
            context.Response,
            _ => { },
            _ => unsubscribed = true,
            cts.Token,
            heartbeatInterval: TimeSpan.FromMinutes(5));

        cts.Cancel();
        await streamTask;

        Assert.True(unsubscribed);
    }
}
