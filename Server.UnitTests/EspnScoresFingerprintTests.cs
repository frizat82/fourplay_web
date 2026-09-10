using FourPlayWebApp.Server.Services;
using FourPlayWebApp.Shared.Models;
using FourPlayWebApp.Shared.Models.Enum;

namespace FourPlayWebApp.Server.UnitTests;

// frizat-a2u (fast-follow, 2026-09-10): PeriodicRefreshCache only raises Changed — the event that
// drives the SSE push to the browser — when this fingerprint differs from the prior poll. The
// original fingerprint hashed only event id + score + status, so a live game sitting through any
// stretch of play where the score itself didn't change (most plays) never fired a push at all,
// even though the clock, down/distance, and ball position kept changing in reality — "staring at
// the page and it never updated" with no dropped connection involved. These tests assert the
// fingerprint reacts to every field a viewer can actually see on screen.
public class EspnScoresFingerprintTests {
    private static EspnScores MakeScores(EspnSitutation? situation = null, string displayClock = "8:42", long period = 3) =>
        new() {
            Events = [
                new Event {
                    Id = "1",
                    Competitions = [
                        new Competition {
                            Status = new EspnStatus {
                                Type = new StatusType { Name = TypeName.StatusInProgress, Description = Description.InProgress },
                                DisplayClock = displayClock,
                                Period = period,
                            },
                            Competitors = [
                                new Competitor { HomeAway = HomeAway.Home, Score = 14 },
                                new Competitor { HomeAway = HomeAway.Away, Score = 10 },
                            ],
                            Situation = situation!,
                            Odds = [],
                        },
                    ],
                },
            ],
        };

    private static EspnSitutation MakeSituation(int down = 1, int yardLine = 50, int distance = 10, string possession = "home-team-id", bool? isRedZone = false) =>
        new() {
            Down = down,
            YardLine = yardLine,
            Distance = distance,
            DownDistanceText = $"{down} & {distance}",
            ShortDownDistanceText = $"{down} & {distance}",
            PossessionText = possession,
            Possession = possession,
            IsRedZone = isRedZone,
        };

    [Fact]
    public void Compute_ReturnsSameFingerprint_WhenNothingChanged() {
        var scores = MakeScores(MakeSituation());

        Assert.Equal(EspnScoresFingerprint.Compute(scores), EspnScoresFingerprint.Compute(scores));
    }

    [Fact]
    public void Compute_ChangesFingerprint_WhenScoreChanges() {
        var before = EspnScoresFingerprint.Compute(MakeScores(MakeSituation()));
        var after = MakeScores(MakeSituation());
        after.Events![0].Competitions[0].Competitors[0].Score = 21;

        Assert.NotEqual(before, EspnScoresFingerprint.Compute(after));
    }

    // The actual bug: same score, same status, but down/distance/yard line changed (a normal
    // non-scoring play) — the OLD fingerprint would report no change here, silently swallowing the
    // SSE push for every non-scoring play in the game. Down and Distance isolated separately so a
    // future edit dropping just one of the two from Compute() still fails a test.
    [Fact]
    public void Compute_ChangesFingerprint_WhenDownChanges() {
        var before = EspnScoresFingerprint.Compute(MakeScores(MakeSituation(down: 1)));
        var after = EspnScoresFingerprint.Compute(MakeScores(MakeSituation(down: 2)));

        Assert.NotEqual(before, after);
    }

    [Fact]
    public void Compute_ChangesFingerprint_WhenDistanceChanges() {
        var before = EspnScoresFingerprint.Compute(MakeScores(MakeSituation(distance: 10)));
        var after = EspnScoresFingerprint.Compute(MakeScores(MakeSituation(distance: 7)));

        Assert.NotEqual(before, after);
    }

    [Fact]
    public void Compute_ChangesFingerprint_WhenYardLineChanges() {
        var before = EspnScoresFingerprint.Compute(MakeScores(MakeSituation(yardLine: 50)));
        var after = EspnScoresFingerprint.Compute(MakeScores(MakeSituation(yardLine: 45)));

        Assert.NotEqual(before, after);
    }

    // Hashes Situation.Possession (the team-id field GameSituationDto.FromSituation treats as
    // authoritative for "who has the ball"), not the derived PossessionText display string.
    [Fact]
    public void Compute_ChangesFingerprint_WhenPossessionChanges() {
        var before = EspnScoresFingerprint.Compute(MakeScores(MakeSituation(possession: "home-team-id")));
        var after = EspnScoresFingerprint.Compute(MakeScores(MakeSituation(possession: "away-team-id")));

        Assert.NotEqual(before, after);
    }

    [Fact]
    public void Compute_ChangesFingerprint_WhenRedZoneChanges() {
        var before = EspnScoresFingerprint.Compute(MakeScores(MakeSituation(isRedZone: false)));
        var after = EspnScoresFingerprint.Compute(MakeScores(MakeSituation(isRedZone: true)));

        Assert.NotEqual(before, after);
    }

    // The clock ticking down between snaps, with no score/down/distance change yet, is itself a
    // real on-screen change (the "Q3 8:42" status line) that a viewer would notice go stale.
    [Fact]
    public void Compute_ChangesFingerprint_WhenClockChanges() {
        var before = EspnScoresFingerprint.Compute(MakeScores(MakeSituation(), displayClock: "8:42"));
        var after = EspnScoresFingerprint.Compute(MakeScores(MakeSituation(), displayClock: "8:15"));

        Assert.NotEqual(before, after);
    }

    [Fact]
    public void Compute_ChangesFingerprint_WhenPeriodChanges() {
        var before = EspnScoresFingerprint.Compute(MakeScores(MakeSituation(), period: 2));
        var after = EspnScoresFingerprint.Compute(MakeScores(MakeSituation(), period: 3));

        Assert.NotEqual(before, after);
    }

    // ESPN's live feed genuinely omits the full situation sub-object sometimes (see frizat-66c) —
    // the fingerprint must not throw when Situation is null, and a null-to-present (or vice versa)
    // transition should itself count as a change.
    [Fact]
    public void Compute_DoesNotThrow_WhenSituationIsNull() {
        var scores = MakeScores(situation: null);

        var ex = Record.Exception(() => EspnScoresFingerprint.Compute(scores));

        Assert.Null(ex);
    }

    [Fact]
    public void Compute_ChangesFingerprint_WhenSituationGoesFromNullToPresent() {
        var before = EspnScoresFingerprint.Compute(MakeScores(situation: null));
        var after = EspnScoresFingerprint.Compute(MakeScores(MakeSituation()));

        Assert.NotEqual(before, after);
    }
}
