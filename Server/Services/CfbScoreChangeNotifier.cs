using FourPlayWebApp.Shared.Models.Data;
using FourPlayWebApp.Server.Services.Interfaces;

namespace FourPlayWebApp.Server.Services;

public class CfbScoreChangeNotifier : ICfbScoreChangeNotifier {
    private readonly object _lock = new();
    private string? _lastFingerprint;

    public event Action? ScoresChanged;

    public void NotifyIfChanged(IEnumerable<CfbScores> scores) {
        var list = scores.ToList();
        if (list.Count == 0) return;

        var fp = string.Join("|", list
            .OrderBy(s => s.EspnEventId)
            .Select(s => $"{s.EspnEventId}:{s.HomeTeamScore}:{s.AwayTeamScore}:{s.GameStatus}"));

        bool changed;
        lock (_lock) {
            changed = fp != _lastFingerprint;
            if (changed) _lastFingerprint = fp;
        }
        if (changed) ScoresChanged?.Invoke();
    }
}
