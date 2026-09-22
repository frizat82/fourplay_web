using System.Threading.Channels;
using Microsoft.AspNetCore.Http;

namespace FourPlayWebApp.Server.Infrastructure;

public static class SseHelper {
    public const string ScoresUpdatedMessage = "data: scores-updated\n\n";
    // A real `data:` event, not a bare SSE comment (`: heartbeat`) — a comment line never fires
    // the browser's EventSource.onmessage at all, so the client-side watchdog in
    // useReconnectingEventSource.ts (which resets on every message, heartbeat included, to detect
    // a connection that's gone silently dead with no onerror) would never see it.
    public const string HeartbeatMessage = "data: heartbeat\n\n";

    public static async Task StreamAsync(
        HttpResponse response,
        Action<Action> subscribe,
        Action<Action> unsubscribe,
        CancellationToken ct,
        TimeSpan? heartbeatInterval = null) {

        response.Headers["Content-Type"] = "text/event-stream";
        response.Headers["Cache-Control"] = "no-cache";
        response.Headers["X-Accel-Buffering"] = "no";

        var channel = Channel.CreateBounded<string>(new BoundedChannelOptions(10) {
            FullMode = BoundedChannelFullMode.DropOldest,
        });

        void OnChanged() => channel.Writer.TryWrite(ScoresUpdatedMessage);

        subscribe(OnChanged);
        try {
            using var heartbeat = new PeriodicTimer(heartbeatInterval ?? TimeSpan.FromSeconds(28));
            _ = Task.Run(async () => {
                while (await heartbeat.WaitForNextTickAsync(ct))
                    channel.Writer.TryWrite(HeartbeatMessage);
            }, ct);

            await foreach (var msg in channel.Reader.ReadAllAsync(ct)) {
                await response.WriteAsync(msg, ct);
                await response.Body.FlushAsync(ct);
            }
        } catch (OperationCanceledException) {
        } finally {
            unsubscribe(OnChanged);
        }
    }
}
