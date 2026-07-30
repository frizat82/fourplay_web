using FourPlayWebApp.Server.Services;
using FourPlayWebApp.Server.Services.Interfaces;
using FourPlayWebApp.Shared.Models;
using FourPlayWebApp.Shared.Models.Enum;

namespace FourPlayWebApp.Server.UnitTests;

/// <summary>
/// frizat-703.6: ReplayCacheService serves an ordered sequence of real captured ESPN snapshots
/// (scheduled -> halftime -> in_progress -> final, see sample_espn_nfl_*.json at the repo root,
/// captured frizat-703.5) and advances on an explicit external trigger rather than a timer — the
/// live click-through E2E test drives state transitions on demand instead of waiting for a real
/// game. Implements both IEspnCacheService and ICfbCacheService (identical shape post-703.6
/// unification) so ONE replay sequence can back either sport's live-data endpoints and SSE.
/// </summary>
public class ReplayCacheServiceTests {
    private static EspnScores Snapshot(string label) => new() {
        Events = [new Event { Id = "1", Competitions = [new Competition {
            Status = new EspnStatus { Type = new StatusType { Name = TypeName.StatusScheduled } },
            Competitors = [], Odds = [],
        }] }],
        // Season used purely to distinguish snapshots by identity in these tests.
        Season = new Season { Year = int.Parse(label) },
    };

    private static ReplayCacheService BuildService(params EspnScores[] snapshots) => new(snapshots);

    [Fact]
    public async Task GetScoresAsync_InitiallyReturnsFirstSnapshot() {
        var svc = BuildService(Snapshot("1"), Snapshot("2"), Snapshot("3"));

        var result = await svc.GetScoresAsync();

        Assert.Equal(1, result!.Season!.Year);
    }

    [Fact]
    public async Task Advance_MovesToNextSnapshot() {
        var svc = BuildService(Snapshot("1"), Snapshot("2"), Snapshot("3"));

        svc.Advance();
        var result = await svc.GetScoresAsync();

        Assert.Equal(2, result!.Season!.Year);
    }

    [Fact]
    public async Task Advance_PastLastSnapshot_StaysAtLast() {
        var svc = BuildService(Snapshot("1"), Snapshot("2"));

        svc.Advance();
        svc.Advance();
        svc.Advance();
        var result = await svc.GetScoresAsync();

        Assert.Equal(2, result!.Season!.Year);
    }

    [Fact]
    public void Advance_FiresScoresChanged() {
        var svc = BuildService(Snapshot("1"), Snapshot("2"));
        int fireCount = 0;
        svc.ScoresChanged += () => fireCount++;

        svc.Advance();

        Assert.Equal(1, fireCount);
    }

    [Fact]
    public void Advance_PastLastSnapshot_DoesNotFireAgain() {
        var svc = BuildService(Snapshot("1"), Snapshot("2"));
        int fireCount = 0;
        svc.Advance(); // -> snapshot 2
        svc.ScoresChanged += () => fireCount++;

        svc.Advance(); // already at last — no-op, no fire

        Assert.Equal(0, fireCount);
    }

    [Fact]
    public async Task Reset_ReturnsToFirstSnapshot() {
        var svc = BuildService(Snapshot("1"), Snapshot("2"), Snapshot("3"));
        svc.Advance();
        svc.Advance();

        svc.Reset();
        var result = await svc.GetScoresAsync();

        Assert.Equal(1, result!.Season!.Year);
    }

    [Fact]
    public void Reset_FiresScoresChanged() {
        var svc = BuildService(Snapshot("1"), Snapshot("2"));
        svc.Advance();
        int fireCount = 0;
        svc.ScoresChanged += () => fireCount++;

        svc.Reset();

        Assert.Equal(1, fireCount);
    }

    [Fact]
    public void Reset_AlreadyAtFirstSnapshot_DoesNotFire() {
        var svc = BuildService(Snapshot("1"), Snapshot("2"));
        int fireCount = 0;
        svc.ScoresChanged += () => fireCount++;

        svc.Reset(); // already at index 0 — no-op, no fire

        Assert.Equal(0, fireCount);
    }

    [Fact]
    public async Task ImplementsBothEspnAndCfbCacheServiceInterfaces() {
        var svc = BuildService(Snapshot("1"));

        // Compile-time proof, not just a shape check: this assigns to the REAL interfaces, so the
        // test fails to build (not just fails at runtime) if ReplayCacheService ever stops
        // implementing either one — exactly the guarantee Program.cs's DI registration relies on.
        IEspnCacheService asEspn = svc;
        ICfbCacheService asCfb = svc;

        Assert.NotNull(await asEspn.GetScoresAsync());
        Assert.NotNull(await asCfb.GetScoresAsync());
    }
}
