using FourPlayWebApp.Shared.Models;
using FourPlayWebApp.Shared.Models.Enum;

namespace FourPlayWebApp.Server.Services;

/// <summary>
/// Holds the last-seen live score snapshot per sport so LivePickTransitionService can diff per-game
/// instead of re-scanning every live pick on every poll. Registered as a singleton with zero
/// dependencies of its own (unlike LivePickTransitionService, which is Scoped so it can take its
/// repository/dispatcher dependencies by ordinary constructor injection) — this is the one piece of
/// state that must survive across the per-recompute DI scopes LivePickNotificationWatcher creates.
/// </summary>
public class LiveScoreSnapshotStore
{
    private EspnScores? _nfl;
    private EspnScores? _cfb;

    /// <summary>Atomically swaps in the new snapshot and returns the previous one (null on the first call).</summary>
    public EspnScores? Exchange(LeagueType sport, EspnScores newValue) =>
        sport == LeagueType.Nfl ? Interlocked.Exchange(ref _nfl, newValue) : Interlocked.Exchange(ref _cfb, newValue);
}
