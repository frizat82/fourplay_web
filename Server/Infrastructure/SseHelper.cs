using System.Threading.Channels;
using Microsoft.AspNetCore.Http;

namespace FourPlayWebApp.Server.Infrastructure;

public static class SseHelper {
    public static async Task StreamAsync(
        HttpResponse response,
        Action<Action> subscribe,
        Action<Action> unsubscribe,
        CancellationToken ct) {

        response.Headers["Content-Type"] = "text/event-stream";
        response.Headers["Cache-Control"] = "no-cache";
        response.Headers["X-Accel-Buffering"] = "no";

        var channel = Channel.CreateBounded<string>(new BoundedChannelOptions(10) {
            FullMode = BoundedChannelFullMode.DropOldest,
        });

        void OnChanged() => channel.Writer.TryWrite("data: scores-updated\n\n");

        subscribe(OnChanged);
        try {
            using var heartbeat = new PeriodicTimer(TimeSpan.FromSeconds(28));
            _ = Task.Run(async () => {
                while (await heartbeat.WaitForNextTickAsync(ct))
                    channel.Writer.TryWrite(": heartbeat\n\n");
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
