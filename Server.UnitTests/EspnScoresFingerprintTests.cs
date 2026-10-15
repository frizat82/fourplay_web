using System.Reflection;
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

    // frizat-6u6: the fingerprint used to hash a hand-picked list of fields, so a field the UI
    // started showing (e.g. timeouts) never triggered a push until someone remembered to add it.
    // Now every field of the fetched week counts: change any one of them in a real captured
    // in-progress scoreboard and the fingerprint must move.
    [Fact]
    public void Compute_Changes_WhenAnyFieldOfTheWeekChanges() {
        var missed = new List<string>();
        var baseline = EspnScoresFingerprint.Compute(LoadInProgress());

        foreach (var path in LeafPaths(LoadInProgress(), "", typeof(EspnScores))) {
            var scores = LoadInProgress();
            if (!TryMutate(scores, path)) continue;
            if (EspnScoresFingerprint.Compute(scores) == baseline) missed.Add(path);
        }

        Assert.Empty(missed);
    }

    private static EspnScores LoadInProgress() {
        using var stream = typeof(EspnScoresFingerprint).Assembly.GetManifestResourceStream("sample_espn_nfl_in_progress.json")!;
        return System.Text.Json.JsonSerializer.Deserialize<EspnScores>(stream, EspnApiServiceJsonConverter.Settings)!;
    }

    private static readonly Type[] LeafTypes = [typeof(string), typeof(bool), typeof(int), typeof(long), typeof(double),
        typeof(decimal), typeof(DateTime), typeof(DateTimeOffset), typeof(Uri)];

    private static bool IsLeaf(Type t) {
        var u = Nullable.GetUnderlyingType(t) ?? t;
        return u.IsEnum || LeafTypes.Contains(u);
    }

    private static IEnumerable<PropertyInfo> Settable(Type t) =>
        t.GetProperties(BindingFlags.Public | BindingFlags.Instance).Where(p => p.CanRead && p.CanWrite && p.GetIndexParameters().Length == 0);

    // Every settable leaf reachable from a populated object, as "Prop/Prop[0]/Prop" paths
    // (first element of each array — every element is serialized the same way).
    private static IEnumerable<string> LeafPaths(object? node, string prefix, Type type) {
        if (node is null) yield break;
        foreach (var prop in Settable(type)) {
            var path = prefix + "/" + prop.Name;
            var value = prop.GetValue(node);
            if (IsLeaf(prop.PropertyType)) { yield return path; continue; }
            if (value is Array arr) {
                if (arr.Length > 0 && arr.GetValue(0) is { } first) {
                    if (IsLeaf(first.GetType())) continue; // arrays of scalars: rare, not walked
                    foreach (var p in LeafPaths(first, path + "[0]", first.GetType())) yield return p;
                }
                continue;
            }
            if (value is not null && !prop.PropertyType.IsValueType)
                foreach (var p in LeafPaths(value, path, value.GetType())) yield return p;
        }
    }

    private static bool TryMutate(object root, string path) {
        var parts = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
        object node = root;
        foreach (var part in parts[..^1]) {
            var name = part.TrimEnd(']').Split('[')[0];
            var value = node.GetType().GetProperty(name)!.GetValue(node)!;
            node = part.EndsWith("[0]") ? ((Array)value).GetValue(0)! : value;
        }
        var prop = node.GetType().GetProperty(parts[^1])!;
        var current = prop.GetValue(node);
        var t = Nullable.GetUnderlyingType(prop.PropertyType) ?? prop.PropertyType;
        object? next = t switch {
            _ when t == typeof(string) => (current as string ?? "") + "x",
            _ when t == typeof(bool) => !(bool)(current ?? false),
            _ when t == typeof(int) => (int)(current ?? 0) + 1,
            _ when t == typeof(long) => (long)(current ?? 0L) + 1,
            _ when t == typeof(double) => (double)(current ?? 0d) + 1,
            _ when t == typeof(decimal) => (decimal)(current ?? 0m) + 1,
            _ when t == typeof(DateTime) => ((DateTime)(current ?? DateTime.UnixEpoch)).AddMinutes(1),
            _ when t == typeof(DateTimeOffset) => ((DateTimeOffset)(current ?? DateTimeOffset.UnixEpoch)).AddMinutes(1),
            _ when t == typeof(Uri) => new Uri(((Uri?)current)?.ToString() + "x", UriKind.RelativeOrAbsolute),
            _ when t.IsEnum => Enum.GetValues(t).Cast<object>().FirstOrDefault(v => !Equals(v, current)),
            _ => null,
        };
        if (next is null) return false;
        prop.SetValue(node, next);
        return true;
    }
}
