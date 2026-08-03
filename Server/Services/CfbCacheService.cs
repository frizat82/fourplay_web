using FourPlayWebApp.Server.Services.Interfaces;
using FourPlayWebApp.Server.Services.Repositories.Interfaces;
using FourPlayWebApp.Shared.Models;
using Microsoft.Extensions.DependencyInjection;

namespace FourPlayWebApp.Server.Services;

// Service that periodically refreshes the CURRENT CFB slate's scores from ESPN and caches them in
// memory, so concurrent controller requests share one poll instead of each triggering its own
// ESPN call — the same problem EspnCacheService solves for NFL. Wraps the same shared
// PeriodicRefreshCache engine (frizat-703.6); differs from NFL only in how it resolves "what to
// fetch" (current CFB slate + CFP/ranked-team filtering via ICfbLiveScoreFetcher).
//
// ICfbCurrentSlateService/ICfbRepository are registered Scoped (matches their use in per-request
// controllers), but this service is a long-lived Singleton — resolving them directly in the
// constructor would be a captive-dependency DI violation. Creates a fresh scope per refresh
// instead, the standard pattern for a singleton background service consuming scoped dependencies.
public class CfbCacheService : ICfbCacheService, IAsyncDisposable
{
    private readonly PeriodicRefreshCache<EspnScores> _cache;

    public event Action? ScoresChanged
    {
        add => _cache.Changed += value;
        remove => _cache.Changed -= value;
    }

    public CfbCacheService(
        IServiceScopeFactory scopeFactory,
        ICfbLiveScoreFetcher fetcher,
        TimeSpan? initialDelay = null)
    {
        _cache = new PeriodicRefreshCache<EspnScores>(
            fetch: async () => {
                using var scope = scopeFactory.CreateScope();
                var currentSlateService = scope.ServiceProvider.GetRequiredService<ICfbCurrentSlateService>();
                var cfbRepo = scope.ServiceProvider.GetRequiredService<ICfbRepository>();

                var currentSlate = await currentSlateService.GetCurrentSlateAsync();
                if (currentSlate is null) return null;

                var slate = await cfbRepo.GetSlateByIdAsync(currentSlate.Id);
                return slate is null ? null : await fetcher.FetchForSlateAsync(slate);
            },
            fingerprint: EspnScoresFingerprint.Compute,
            interval: TimeSpan.FromMinutes(5),
            initialDelay: initialDelay);
    }

    public Task<EspnScores?> GetScoresAsync() => Task.FromResult(_cache.Current);

    public ValueTask DisposeAsync() => _cache.DisposeAsync();
}
