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
        var body = new SignalingStream();
        context.Response.Body = body;
        using var cts = new CancellationTokenSource();

        var streamTask = SseHelper.StreamAsync(
            context.Response,
            _ => { },
            _ => { },
            cts.Token,
            heartbeatInterval: TimeSpan.FromMilliseconds(15));

        // Wait for the heartbeat to actually be written, then cancel — not a fixed sleep, which
        // raced the write on a loaded CI runner (CI-observed: body came back empty).
        await body.WaitForAsync(SseHelper.HeartbeatMessage);
        cts.Cancel();
        await streamTask;

        var written = body.Text;
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
        var body = new SignalingStream();
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
        // Reading the channel, writing, and flushing happen on a background continuation, not
        // inside Invoke() — wait for the write itself. A fixed sleep (20ms, then 150ms) still lost
        // the race on a loaded CI runner: body came back empty.
        await body.WaitForAsync(SseHelper.ScoresUpdatedMessage);
        cts.Cancel();
        await streamTask;

        var written = body.Text;
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

    // Response body that lets a test await specific text being written, so assertions follow the
    // write itself instead of a wall-clock guess about when a background continuation runs.
    private sealed class SignalingStream : MemoryStream {
        private readonly object _gate = new();
        private readonly List<(string Text, TaskCompletionSource Done)> _waiters = [];

        public string Text { get { lock (_gate) return Encoding.UTF8.GetString(ToArray()); } }

        public override void Write(byte[] buffer, int offset, int count) {
            lock (_gate) {
                base.Write(buffer, offset, count);
                var text = Encoding.UTF8.GetString(ToArray());
                foreach (var w in _waiters.Where(w => text.Contains(w.Text)).ToList()) {
                    w.Done.TrySetResult();
                    _waiters.Remove(w);
                }
            }
        }

        public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken ct) {
            Write(buffer, offset, count);
            return Task.CompletedTask;
        }

        public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken ct = default) {
            Write(buffer.ToArray(), 0, buffer.Length);
            return ValueTask.CompletedTask;
        }

        public async Task WaitForAsync(string text) {
            Task done;
            lock (_gate) {
                if (Encoding.UTF8.GetString(ToArray()).Contains(text)) return;
                var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                _waiters.Add((text, tcs));
                done = tcs.Task;
            }
            // Generous ceiling only so a real regression fails instead of hanging the run.
            await done.WaitAsync(TimeSpan.FromSeconds(30));
        }
    }
}

