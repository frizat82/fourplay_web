using System.Reflection;
using FourPlayWebApp.Server.Services;
using FourPlayWebApp.Shared.Models;

namespace FourPlayWebApp.Server.UnitTests;

// PeriodicRefreshCache only raises Changed — the SSE push to the browser and the live pick
// notifications — when this fingerprint differs from the prior poll. It used to hash a hand-picked
// list of fields, so a field missing from the list never pushed (frizat-a2u: down/distance; still
// true of timeouts). frizat-6u6: it now hashes the whole fetched week, and these tests hold it to
// that — every field counts.
public class EspnScoresFingerprintTests {
    private static EspnScores LoadInProgress() => DemoFixtureLoader.Load("sample_espn_nfl_in_progress.json")!;

    [Fact]
    public void Compute_ReturnsSameFingerprint_WhenNothingChanged() {
        Assert.Equal(EspnScoresFingerprint.Compute(LoadInProgress()), EspnScoresFingerprint.Compute(LoadInProgress()));
    }

    // Change any one field of a real captured in-progress scoreboard and the fingerprint must move.
    // Sub-objects the capture doesn't have (odds, weather, rankings...) are filled in first, so
    // every field the model maps is reached — including ones added later.
    [Fact]
    public void Compute_Changes_WhenAnyFieldOfTheWeekChanges() {
        var scores = LoadInProgress();
        FillMissing(scores, []);
        var baseline = EspnScoresFingerprint.Compute(scores);
        var visited = new List<string>();
        var missed = new List<string>();

        foreach (var (owner, prop, path) in Leaves(scores, "")) {
            var original = prop.GetValue(owner);
            if (Mutated(prop.PropertyType, original) is not { } next) continue;
            visited.Add(path);
            prop.SetValue(owner, next);
            if (EspnScoresFingerprint.Compute(scores) == baseline) missed.Add(path);
            prop.SetValue(owner, original);
        }

        Assert.Empty(missed);
        Assert.Contains(visited, p => p.Contains("/Odds[0]/"));
        Assert.Contains(visited, p => p.Contains("/Weather/"));
        Assert.Contains(visited, p => p.Contains("/CuratedRank/"));
    }

    // Gives every null sub-object and empty array one default instance, recursively (a type
    // already on the current path is left alone, so self-referencing types can't recurse forever).
    private static void FillMissing(object node, HashSet<Type> onPath) {
        if (!onPath.Add(node.GetType())) return;
        foreach (var prop in node.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance)
                     .Where(p => p.CanWrite && p.GetIndexParameters().Length == 0 && !IsLeaf(p.PropertyType))) {
            var value = prop.GetValue(node);
            if (prop.PropertyType.IsArray) {
                var elementType = prop.PropertyType.GetElementType()!;
                if (IsLeaf(elementType) || onPath.Contains(elementType)) continue;
                if (value is not Array { Length: > 0 }) {
                    if (elementType.GetConstructor(Type.EmptyTypes) is null) continue;
                    var arr = Array.CreateInstance(elementType, 1);
                    arr.SetValue(Activator.CreateInstance(elementType), 0);
                    prop.SetValue(node, value = arr);
                }
                if (((Array)value).GetValue(0) is { } first) FillMissing(first, onPath);
                continue;
            }
            if (prop.PropertyType.IsValueType || onPath.Contains(prop.PropertyType)) continue;
            if (value is null) {
                if (prop.PropertyType.GetConstructor(Type.EmptyTypes) is null) continue;
                prop.SetValue(node, value = Activator.CreateInstance(prop.PropertyType));
            }
            FillMissing(value!, onPath);
        }
        onPath.Remove(node.GetType());
    }

    // ESPN's live feed sometimes omits the situation sub-object (frizat-66c): no throw, and its
    // appearing or disappearing counts as a change.
    [Fact]
    public void Compute_HandlesASituationThatComesAndGoes() {
        var scores = LoadInProgress();
        var competition = scores.Events![0].Competitions[0];
        var withSituation = EspnScoresFingerprint.Compute(scores);

        competition.Situation = null!;

        Assert.NotEqual(withSituation, EspnScoresFingerprint.Compute(scores));
    }

    private static readonly Type[] LeafTypes = [typeof(string), typeof(bool), typeof(int), typeof(long), typeof(double),
        typeof(decimal), typeof(DateTime), typeof(DateTimeOffset), typeof(Uri)];

    private static bool IsLeaf(Type t) {
        var u = Nullable.GetUnderlyingType(t) ?? t;
        return u.IsEnum || LeafTypes.Contains(u);
    }

    // Every settable leaf reachable from a populated object (first element of each array — every
    // element is serialized the same way), with its path for the failure message.
    private static IEnumerable<(object Owner, PropertyInfo Prop, string Path)> Leaves(object node, string prefix) {
        foreach (var prop in node.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance)
                     .Where(p => p.CanWrite && p.GetIndexParameters().Length == 0)) {
            var path = $"{prefix}/{prop.Name}";
            if (IsLeaf(prop.PropertyType)) { yield return (node, prop, path); continue; }
            var child = prop.GetValue(node) switch {
                Array { Length: > 0 } arr => arr.GetValue(0),
                Array => null,
                var v => v,
            };
            if (child is null || IsLeaf(child.GetType())) continue;
            foreach (var leaf in Leaves(child, prop.PropertyType.IsArray ? path + "[0]" : path)) yield return leaf;
        }
    }

    private static object? Mutated(Type type, object? current) {
        var t = Nullable.GetUnderlyingType(type) ?? type;
        return t switch {
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
    }
}
