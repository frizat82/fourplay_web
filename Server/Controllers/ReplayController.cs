using FourPlayWebApp.Server.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FourPlayWebApp.Server.Controllers;

/// <summary>
/// Test-only: advances ReplayCacheService to the next captured real-game snapshot on demand, so
/// the live click-through E2E test can drive state transitions without waiting for a real game
/// (frizat-703.6). Only functional when DEMO_REPLAY_MODE=true, where ReplayCacheService is
/// registered — resolved via IServiceProvider rather than constructor injection so this
/// controller doesn't fail DI resolution in any other mode.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "Administrator")]
public class ReplayController(IServiceProvider serviceProvider) : ControllerBase {
    private IActionResult RunOnReplayService(Action<ReplayCacheService> action, string successMessage) {
        var replayService = serviceProvider.GetService<ReplayCacheService>();
        if (replayService is null)
            return NotFound(new { message = "Replay mode is not enabled (DEMO_REPLAY_MODE != true)." });

        action(replayService);
        return Ok(new { message = successMessage });
    }

    [HttpPost("advance")]
    public IActionResult Advance() =>
        RunOnReplayService(s => s.Advance(), "Advanced to next replay snapshot");

    // The NFL and CFB E2E specs share this one replay sequence (see ReplayCacheService) — each
    // calls this before picking so it can run standalone or after the other without inheriting
    // whatever snapshot index the other left it at.
    [HttpPost("reset")]
    public IActionResult Reset() =>
        RunOnReplayService(s => s.Reset(), "Reset to first replay snapshot");
}
