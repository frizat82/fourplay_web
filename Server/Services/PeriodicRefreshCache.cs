using Serilog;

namespace FourPlayWebApp.Server.Services;

/// <summary>
/// Generic periodic-refresh cache: polls a fetch delegate, exposes the latest value, and raises
/// Changed exactly when a refresh produces a different fingerprint than the last one. Shared by
/// NFL (EspnCacheService) and CFB (CfbCacheService, frizat-703.6) so both sports use identical
/// caching/change-detection machinery — only the fetch delegate differs per sport; both currently
/// pass the same EspnScoresFingerprint.Compute (frizat-703.6 unification).
///
/// The wait between refreshes is computed fresh after each one via intervalSelector(Current) —
/// not a fixed TimeSpan — so a caller can poll faster while something time-sensitive is happening
/// (frizat-ucv: e.g. EspnCacheService polling ESPN faster during a live game) and slower otherwise,
/// without this generic engine knowing anything about what "faster" means for its T. A caller that
/// wants the old fixed-interval behavior just passes a selector that ignores its input and always
/// returns the same TimeSpan. PeriodicTimer can't have its period changed after construction, so
/// this uses a plain Task.Delay loop instead.
/// </summary>
public sealed class PeriodicRefreshCache<T> : IAsyncDisposable where T : class
{
    private readonly Func<Task<T?>> _fetch;
    private readonly Func<T, string> _fingerprint;
    private readonly Func<T?, TimeSpan> _intervalSelector;
    private readonly TimeSpan _initialDelay;
    private readonly CancellationTokenSource _cts = new();
    private string? _lastFingerprint;

    public event Action? Changed;
    public T? Current { get; private set; }

    public PeriodicRefreshCache(
        Func<Task<T?>> fetch,
        Func<T, string> fingerprint,
        Func<T?, TimeSpan> intervalSelector,
        TimeSpan? initialDelay = null)
    {
        _fetch = fetch;
        _fingerprint = fingerprint;
        _intervalSelector = intervalSelector;
        _initialDelay = initialDelay ?? TimeSpan.Zero;
        _ = RefreshLoopAsync();
    }

    private async Task RefreshLoopAsync()
    {
        await RefreshAsync();
        try
        {
            while (!_cts.IsCancellationRequested)
            {
                await Task.Delay(_intervalSelector(Current), _cts.Token);
                await RefreshAsync();
            }
        }
        catch (OperationCanceledException)
        {
            // Cancelled — expected on dispose.
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
    }
}
