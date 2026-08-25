using FourPlayWebApp.Server.Controllers;
using FourPlayWebApp.Shared.Models.Data.Dtos;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using NSubstitute;
using Xunit;

namespace FourPlayWebApp.Server.UnitTests;

public class VersionControllerTests
{
    // /code-review: this repo deploys via Railway/Vercel's native git integration, not a custom
    // GitHub-Actions-driven deploy step — GITHUB_SHA is never actually populated at runtime on
    // either platform. Railway auto-injects RAILWAY_GIT_COMMIT_SHA for every git-connected deploy,
    // so that's the primary source; GITHUB_SHA stays as a secondary fallback for any environment
    // that does set it directly (e.g. a future custom deploy step), then "local" last.
    private static VersionController BuildController(string? railwaySha, string? githubSha, string environmentName) {
        var config = Substitute.For<IConfiguration>();
        config["RAILWAY_GIT_COMMIT_SHA"].Returns(railwaySha);
        config["GITHUB_SHA"].Returns(githubSha);
        var env = Substitute.For<IWebHostEnvironment>();
        env.EnvironmentName.Returns(environmentName);
        return new VersionController(config, env);
    }

    [Fact]
    public void GetVersion_Returns200_WithShaAndEnv() {
        var controller = BuildController(railwaySha: null, githubSha: "abc1234def5678", "Production");

        var result = controller.GetVersion();

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var value = Assert.IsType<VersionDto>(ok.Value);
        Assert.Equal("abc1234def5678", value.Sha);
        Assert.Equal("Production", value.Env);
    }

    [Fact]
    public void GetVersion_PrefersRailwayGitCommitSha_OverGithubSha() {
        var controller = BuildController(railwaySha: "railway-sha-999", githubSha: "abc1234def5678", "Production");

        var result = controller.GetVersion();

        var value = Assert.IsType<VersionDto>(((OkObjectResult)result.Result!).Value);
        Assert.Equal("railway-sha-999", value.Sha);
    }

    [Fact]
    public void GetVersion_WhenEnvVarMissing_ReturnsFallback() {
        var controller = BuildController(railwaySha: null, githubSha: null, "Development");

        var result = controller.GetVersion();

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var value = Assert.IsType<VersionDto>(ok.Value);
        Assert.Equal("local", value.Sha);
        Assert.Equal("Development", value.Env);
    }

    // /code-review: a plain `??` chain treats an explicitly-set empty string as a real value,
    // not a missing one — some platforms/scripts export an unset build var as "" rather than
    // omitting it, which would otherwise permanently show every client the stale-build banner.
    [Fact]
    public void GetVersion_WhenEnvVarIsEmptyString_FallsThroughToNextSource() {
        var controller = BuildController(railwaySha: "", githubSha: "abc1234def5678", "Production");

        var result = controller.GetVersion();

        var value = Assert.IsType<VersionDto>(((OkObjectResult)result.Result!).Value);
        Assert.Equal("abc1234def5678", value.Sha);
    }
}
