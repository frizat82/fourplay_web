using FourPlayWebApp.Server.Services;
using FourPlayWebApp.Shared.Models.Data;

namespace FourPlayWebApp.Server.UnitTests;

public class CfbScoreChangeNotifierTests
{
    private static CfbScores MakeScore(int id, int home, int away, string status) =>
        new() { EspnEventId = id, HomeTeamScore = home, AwayTeamScore = away, GameStatus = status };

    [Fact]
    public void NotifyIfChanged_FiresEvent_OnFirstCallWithScores()
    {
        var notifier = new CfbScoreChangeNotifier();
        int fired = 0;
        notifier.ScoresChanged += () => fired++;

        notifier.NotifyIfChanged([MakeScore(1, 0, 0, "StatusInProgress")]);

        Assert.Equal(1, fired);
    }

    [Fact]
    public void NotifyIfChanged_DoesNotFire_WhenListIsEmpty()
    {
        var notifier = new CfbScoreChangeNotifier();
        int fired = 0;
        notifier.ScoresChanged += () => fired++;

        notifier.NotifyIfChanged([]);

        Assert.Equal(0, fired);
    }

    [Fact]
    public void NotifyIfChanged_FiresAgain_WhenScoresChange()
    {
        var notifier = new CfbScoreChangeNotifier();
        int fired = 0;
        notifier.ScoresChanged += () => fired++;

        notifier.NotifyIfChanged([MakeScore(1, 0, 0, "StatusInProgress")]);
        notifier.NotifyIfChanged([MakeScore(1, 21, 14, "StatusFinal")]);

        Assert.Equal(2, fired);
    }

    [Fact]
    public void NotifyIfChanged_DoesNotFire_WhenScoresIdentical()
    {
        var notifier = new CfbScoreChangeNotifier();
        int fired = 0;
        notifier.ScoresChanged += () => fired++;

        var scores = new List<CfbScores> { MakeScore(1, 21, 14, "StatusFinal") };

        notifier.NotifyIfChanged(scores);
        notifier.NotifyIfChanged(scores);

        Assert.Equal(1, fired);
    }

    [Fact]
    public void NotifyIfChanged_FiresOnce_WhenSameScoresPassedAsNewList()
    {
        var notifier = new CfbScoreChangeNotifier();
        int fired = 0;
        notifier.ScoresChanged += () => fired++;

        notifier.NotifyIfChanged([MakeScore(1, 21, 14, "StatusFinal")]);
        notifier.NotifyIfChanged([MakeScore(1, 21, 14, "StatusFinal")]);

        Assert.Equal(1, fired);
    }
}
