namespace FourPlayWebApp.Server.UnitTests;

// Fixed, controlled "now" for boundary-condition tests — not tied to the real wall clock. Shared
// across test files instead of each redeclaring its own private copy (NflSpreadJobTests,
// CfbSpreadJobTests, CfbRankingCaptureJobTests, and LeagueJuiceScheduleSourceTests each still carry
// their own pre-existing private copy of this exact class; new tests should reference this one).
public sealed class FakeTimeProvider(DateTimeOffset now) : TimeProvider {
    public override DateTimeOffset GetUtcNow() => now;
}
