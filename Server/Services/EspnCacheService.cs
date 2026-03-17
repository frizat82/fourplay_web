using FourPlayWebApp.Server.Services.Interfaces;
using FourPlayWebApp.Shared.Models;
using Microsoft.Extensions.Caching.Memory;

namespace FourPlayWebApp.Server.Services;
// Service that periodically refreshes ESPN scores from the API and caches them in memory
// This is over the top but it will ensure that scores are cached 1x, if we use IMemoryCache directly
// in the controller there is a chance that many can fire when a cache is expired
public class EspnCacheService : IEspnCacheService, IAsyncDisposable
{
    private readonly IEspnApiService _espnApiService;
    private readonly IMemoryCache _memoryCache;
    private readonly PeriodicTimer? _timer;
    private readonly CancellationTokenSource _cts = new();

    private const string _cacheKey = "espn-scores";

    public EspnCacheService(IEspnApiService espnApiService, IMemoryCache memoryCache)
    {
        _espnApiService = espnApiService;
        _memoryCache = memoryCache;

        // Refresh every 2 minutes
        _timer = new PeriodicTimer(TimeSpan.FromMinutes(5));

        // Fire initial refresh immediately
        _ = RefreshLoopAsync();
    }

    // Public method to get cached scores
    public Task<EspnScores?> GetScoresAsync()
    {
        return Task.FromResult(_memoryCache.Get<EspnScores?>(_cacheKey));
    }

    // Internal loop for periodic refresh
    private async Task RefreshLoopAsync()
    {
        ArgumentNullException.ThrowIfNull(_timer);
        // Immediate first run
        await RefreshScoresAsync();

        try
        {
            while (await _timer.WaitForNextTickAsync(_cts.Token))
            {
                await RefreshScoresAsync();
            }
        }
        catch (OperationCanceledException)
        {
            // Timer cancelled
        }
    }

    private async Task RefreshScoresAsync()
    {
        try
        {
            var scores = await _espnApiService.GetScores();
            if (scores != null)
            {
                _memoryCache.Set(_cacheKey, scores, TimeSpan.FromMinutes(5));
            }
        }
        catch (Exception ex)
        {
            // Optional: log errors but keep last good value
            Console.WriteLine($"Failed to refresh scores: {ex.Message}");
        }
    }

    public async ValueTask DisposeAsync()
    {
        await _cts.CancelAsync();
        _cts.Dispose();

        _timer?.Dispose();
    }
}
