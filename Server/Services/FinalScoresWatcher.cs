using System.Threading.Channels;
using FourPlayWebApp.Server.Services.Interfaces;
using FourPlayWebApp.Shared.Helpers;
using FourPlayWebApp.Shared.Models;
using Microsoft.Extensions.Hosting;
using Quartz;

namespace FourPlayWebApp.Server.Services;

/// <summary>
/// Saves final scores the moment a game ends instead of at the next fixed scores-job time. Those
/// times left gaps of hours on a Sunday — a 3:25pm kickoff's final waited for the 7:40pm run, a
/// Sunday-night game's for 1am — and results, the leaderboard and the money ride on saved finals.
///
/// Listens to both live caches' ScoresChanged events (they fire on every ~15s poll while a game
/// is on). When the scoreboard shows a game final that this process hasn't seen final before, it
/// triggers that sport's trigger-less "... Scores On Final" job (Program.cs). The job reads ESPN
/// fresh and upserts every final, so one run catches every game that ended since the last one.
/// The fixed-time triggers stay as a backstop. One implementation for both sports — the only
/// sport-specific part is which cache and which job key (CLAUDE.md's NFL/CFB sharing rule).
/// </summary>
public class FinalScoresWatcher : BackgroundService
{
    public const string NflJobName = "NFL Scores On Final";
    public const string CfbJobName = "CFB Scores On Final";

    private readonly ISchedulerFactory _schedulerFactory;
    private readonly ILogger<FinalScoresWatcher> _logger;
    private readonly bool _enabled;
    private readonly SportFeed[] _feeds;

    public FinalScoresWatcher(
        IEspnCacheService espnCache,
        ICfbCacheService cfbCache,
        ISchedulerFactory schedulerFactory,
        IConfiguration configuration,
        ILogger<FinalScoresWatcher> logger)
    {
        _schedulerFactory = schedulerFactory;
        _logger = logger;
        // Kill switch: turn off without a deploy; the fixed-time scores triggers still run.
        _enabled = configuration.GetValue("FINAL_SCORES_WATCHER_ENABLED", true);
        _feeds =
        [
            new SportFeed("NFL", new JobKey(NflJobName), espnCache.GetScoresAsync,
                h => espnCache.ScoresChanged += h, h => espnCache.ScoresChanged -= h),
            new SportFeed("CFB", new JobKey(CfbJobName), cfbCache.GetScoresAsync,
                h => cfbCache.ScoresChanged += h, h => cfbCache.ScoresChanged -= h),
        ];
    }

    public override Task StartAsync(CancellationToken cancellationToken)
    {
        if (_enabled)
        {
            foreach (var feed in _feeds) feed.Subscribe();
        }
        else
        {
            _logger.LogInformation("FinalScoresWatcher disabled via FINAL_SCORES_WATCHER_ENABLED=false");
        }
        return base.StartAsync(cancellationToken);
    }

    public override Task StopAsync(CancellationToken cancellationToken)
    {
        foreach (var feed in _feeds) feed.Unsubscribe();
        return base.StopAsync(cancellationToken);
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken) =>
        _enabled ? Task.WhenAll(_feeds.Select(feed => DrainAsync(feed, stoppingToken))) : Task.CompletedTask;

    private async Task DrainAsync(SportFeed feed, CancellationToken stoppingToken)
    {
        // ScoresChanged fires inside the cache's poll loop, so the handler only signals; a burst of
        // polls coalesces into one pending check (capacity-1 channel).
        await foreach (var _ in feed.Signal.Reader.ReadAllAsync(stoppingToken))
        {
            try
            {
                var finals = FinalGameIds(await feed.GetScoresAsync());
                if (finals.IsSubsetOf(feed.SeenFinal)) continue;

                var scheduler = await _schedulerFactory.GetScheduler(stoppingToken);
                // Demo/replay stacks never register the scores jobs — their data is seeded.
                if (await scheduler.CheckExists(feed.JobKey, stoppingToken))
                {
                    _logger.LogInformation("FinalScoresWatcher: {Sport} game went final, saving scores now", feed.Sport);
                    await scheduler.TriggerJob(feed.JobKey, stoppingToken);
                }
                // Only after the run is queued: a failed check leaves the final "new" for next time.
                feed.SeenFinal.UnionWith(finals);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                // The next poll's signal retries; the fixed-time triggers are the backstop.
                _logger.LogWarning(ex, "FinalScoresWatcher: {Sport} check failed, will retry on the next poll", feed.Sport);
            }
        }
    }

    private sealed class SportFeed(
        string sport,
        JobKey jobKey,
        Func<Task<EspnScores?>> getScores,
        Action<Action> subscribe,
        Action<Action> unsubscribe)
    {
        public string Sport { get; } = sport;
        /// <summary>Games this process has already seen final (and queued a save for).</summary>
        public HashSet<string> SeenFinal { get; } = [];
        public JobKey JobKey { get; } = jobKey;
        public Channel<bool> Signal { get; } = Channel.CreateBounded<bool>(
            new BoundedChannelOptions(1) { FullMode = BoundedChannelFullMode.DropWrite });

        public Task<EspnScores?> GetScoresAsync() => getScores();
        public void Subscribe() => subscribe(OnScoresChanged);
        public void Unsubscribe() => unsubscribe(OnScoresChanged);
        private void OnScoresChanged() => Signal.Writer.TryWrite(true);
    }

    private static HashSet<string> FinalGameIds(EspnScores? scores) =>
        (scores?.Events ?? [])
            .SelectMany(e => e.Competitions ?? [])
            .Where(GameHelpers.IsGameOver)
            .Select(c => c.Id)
            .ToHashSet();
}
