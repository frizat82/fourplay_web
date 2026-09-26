using FourPlayWebApp.Shared.Models;
using Microsoft.Extensions.Caching.Memory;

namespace FourPlayWebApp.Server.Services;

/// <summary>
/// Caches each ESPN single-day scoreboard response (and the CFP bucket) for as long as it can't
/// change — shared by the NFL and CFB API services. ESPN only answers single-day queries now, so a
/// week/slate window is one request per day, re-fetched by every poll (every 15s during games —
/// see ScorePollSchedule) and by requests that fall back to their own fetch between polls. With
/// this cache those only reach ESPN for days that can actually have changed. The ESPN-reading jobs
/// opt out with <see cref="Fresh"/>.
/// <para>A failed refetch (ESPN blocking us, down, or timing out) serves the last good copy for up to
/// <see cref="ServeStaleFor"/> — so a live day doesn't vanish from the scoreboard — and is reported
/// to any <see cref="TrackFailures"/> scope, so the poller backs off instead of retrying every 15s.
/// Never inside <see cref="Fresh"/>: the jobs persist what they read.</para>
/// <para>Cached responses are shared by every reader: treat them as read-only (nothing mutates
/// them today — the fetchers build new scoreboards via GameHelpers.WithEvents).</para>
/// </summary>
public sealed class EspnDayCache(IMemoryCache cache, TimeProvider time) {
    /// <summary>A day with a game on: always refetched by the next fast poll.</summary>
    public static readonly TimeSpan LiveTtl = TimeSpan.FromSeconds(10);
    /// <summary>A day with no games — or a response that came back empty; re-checked every 15 min when read.</summary>
    public static readonly TimeSpan EmptyTtl = TimeSpan.FromMinutes(15);
    /// <summary>A day whose games are all final (the scores jobs read fresh), or upcoming games far off.</summary>
    public static readonly TimeSpan SettledTtl = TimeSpan.FromHours(6);

    /// <summary>How long past its freshness a day is kept to fall back on when a refetch fails.</summary>
    public static readonly TimeSpan ServeStaleFor = TimeSpan.FromHours(12);

    private static readonly AsyncLocal<bool> Bypass = new();
    private static readonly AsyncLocal<FailureTracker?> Failures = new();

    private sealed record Entry(EspnScores Day, DateTimeOffset FreshUntil);

    /// <summary>
    /// Within this scope every day is fetched from ESPN (and the cache refreshed with it). For the
    /// jobs that persist what they read — scores, spreads, rankings — which run a few times a week
    /// and must not act on a cached snapshot.
    /// </summary>
    public static IDisposable Fresh() {
        var previous = Bypass.Value;
        Bypass.Value = true;
        return new Restore(previous);
    }

    private sealed class Restore(bool previous) : IDisposable {
        public void Dispose() => Bypass.Value = previous;
    }

    /// <summary>Within this scope, whether any day fetch failed (stale copy served or not).</summary>
    public static FailureTracker TrackFailures() {
        var tracker = new FailureTracker(Failures.Value);
        Failures.Value = tracker;
        return tracker;
    }

    public sealed class FailureTracker(FailureTracker? previous) : IDisposable {
        private volatile bool _anyFailed;
        public bool AnyFailed => _anyFailed;
        internal void Report() => _anyFailed = true;
        public void Dispose() => Failures.Value = previous;
    }

    public async Task<EspnScores?> GetOrFetchAsync(string key, Func<Task<EspnScores?>> fetch) {
        var now = time.GetUtcNow();
        var last = cache.Get<Entry>(key);
        if (!Bypass.Value && last is not null && now < last.FreshUntil) return last.Day;

        EspnScores? day;
        try {
            day = await fetch();
        } catch {
            Failures.Value?.Report();
            if (Stale(last) is { } stale) return stale;
            throw;
        }
        // A failed fetch or a malformed ({}-style, no events array) response isn't cached: the next poll retries.
        if (day?.Events is null) {
            Failures.Value?.Report();
            return Stale(last) ?? day;
        }
        var ttl = TtlFor(day, now);
        cache.Set(key, new Entry(day, now + ttl), ttl + ServeStaleFor);
        return day;
    }

    private static EspnScores? Stale(Entry? last) => Bypass.Value ? null : last?.Day;

    public static TimeSpan TtlFor(EspnScores day, DateTimeOffset now) {
        var games = day.Events?.SelectMany(e => e.Competitions).ToList() ?? [];
        if (games.Count == 0) return EmptyTtl;
        if (games.Any(g => EspnPollCadence.IsLive(g, now))) return LiveTtl;
        var upcoming = games.Where(g => EspnPollCadence.IsUpcoming(g, now)).ToList();
        if (upcoming.Count == 0) return SettledTtl; // all final, or postponed/canceled (ESPN keeps those "scheduled")
        var untilKickoff = upcoming.Min(g => g.Date) - now;
        return untilKickoff < SettledTtl ? untilKickoff : SettledTtl;
    }
}

/// <summary>The API services' optional cache: straight to ESPN when there's none (tests).</summary>
public static class EspnDayCacheExtensions {
    public static Task<EspnScores?> GetOrFetchDayAsync(this EspnDayCache? cache, string key, Func<Task<EspnScores?>> fetch) =>
        cache is null ? fetch() : cache.GetOrFetchAsync(key, fetch);
}
