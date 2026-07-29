using FourPlayWebApp.Shared.Models.Data;

namespace FourPlayWebApp.Server.Services.Interfaces;

public interface ICfbScoreChangeNotifier {
    event Action? ScoresChanged;
    void NotifyIfChanged(IEnumerable<CfbScores> scores);
}
