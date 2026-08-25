using FourPlayWebApp.Shared.Models.Data.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FourPlayWebApp.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
[AllowAnonymous]
[Microsoft.AspNetCore.RateLimiting.EnableRateLimiting("public")]
public class VersionController(IConfiguration config, IWebHostEnvironment environment) : ControllerBase {
    [HttpGet]
    public ActionResult<VersionDto> GetVersion() {
        // Railway (this app's actual deploy target) auto-injects RAILWAY_GIT_COMMIT_SHA on every
        // git-connected deploy — no workflow config needed. GITHUB_SHA is only ever set by a
        // GitHub-Actions-driven deploy step, which this repo doesn't have; kept as a fallback for
        // any environment that does set it directly. IsNullOrEmpty (not a plain ??) because some
        // platforms/scripts export an unset build var as "" rather than omitting it entirely.
        var sha = FirstNonEmpty(config["RAILWAY_GIT_COMMIT_SHA"], config["GITHUB_SHA"]) ?? "local";
        return Ok(new VersionDto(sha, environment.EnvironmentName, DateTimeOffset.UtcNow));
    }

    private static string? FirstNonEmpty(params string?[] values) =>
        values.FirstOrDefault(v => !string.IsNullOrEmpty(v));
}
