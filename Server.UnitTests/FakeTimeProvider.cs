namespace FourPlayWebApp.Server.UnitTests;

// Controlled "now" for boundary-condition tests — not tied to the real wall clock. Shared across
// test files instead of each redeclaring its own private copy (NflSpreadJobTests,
// CfbSpreadJobTests, CfbRankingCaptureJobTests, and LeagueJuiceScheduleSourceTests each still carry
// their own pre-existing private copy of this exact class; new tests should reference this one).
public sealed class FakeTimeProvider(DateTimeOffset now) : TimeProvider {
    private DateTimeOffset _now = now;
    public override DateTimeOffset GetUtcNow() => _now;
    public void Advance(TimeSpan by) => _now += by;
}
