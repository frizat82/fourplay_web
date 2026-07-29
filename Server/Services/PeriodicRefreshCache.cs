using Serilog;

namespace FourPlayWebApp.Server.Services;

/// <summary>
/// Generic periodic-refresh cache: polls a fetch delegate on an interval, exposes the latest
/// value, and raises Changed exactly when a refresh produces a different fingerprint than the
/// last one. Shared by NFL (EspnCacheService) and CFB (CfbCacheService, frizat-703.6) so both
/// sports use identical caching/change-detection machinery — only the fetch delegate and
/// fingerprint function differ per sport (frizat-703.6 unification).
/// </summary>
public sealed class PeriodicRefreshCache<T> : IAsyncDisposable where T : class
{
    private readonly Func<Task<T?>> _fetch;
    private readonly Func<T, string> _fingerprint;
    private readonly TimeSpan _initialDelay;
    private readonly PeriodicTimer _timer;
    private readonly CancellationTokenSource _cts = new();
    private string? _lastFingerprint;

    public event Action? Changed;
    public T? Current { get; private set; }

    public PeriodicRefreshCache(
        Func<Task<T?>> fetch,
        Func<T, string> fingerprint,
        TimeSpan interval,
        TimeSpan? initialDelay = null)
    {
        _fetch = fetch;
        _fingerprint = fingerprint;
        _initialDelay = initialDelay ?? TimeSpan.Zero;
        _timer = new PeriodicTimer(interval);
        _ = RefreshLoopAsync();
    }

    private async Task RefreshLoopAsync()
    {
        await RefreshAsync();
        try
        {
            while (await _timer.WaitForNextTickAsync(_cts.Token))
                await RefreshAsync();
        }
        catch (OperationCanceledException)
        {
            // Timer cancelled — expected on dispose.
        }
    }

    private async Task RefreshAsync()
    {
        if (_initialDelay > TimeSpan.Zero)
            await Task.Delay(_initialDelay);

        try
        {
            var value = await _fetch();
            if (value is null) return;
            Current = value;

            var fp = _fingerprint(value);
            if (fp != _lastFingerprint)
            {
                _lastFingerprint = fp;
                Changed?.Invoke();
            }
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "PeriodicRefreshCache<{Type}> refresh failed", typeof(T).Name);
        }
    }

    public async ValueTask DisposeAsync()
    {
        await _cts.CancelAsync();
        _cts.Dispose();
        _timer.Dispose();
    }
}
