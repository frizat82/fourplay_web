using FourPlayWebApp.Server.Controllers;
using FourPlayWebApp.Server.Services;
using FourPlayWebApp.Shared.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;

namespace FourPlayWebApp.Server.UnitTests;

public class ReplayControllerTests {
    [Fact]
    public void Advance_WhenReplayServiceRegistered_AdvancesAndReturnsOk() {
        var replayService = new ReplayCacheService([new EspnScores(), new EspnScores()]);
        var services = new ServiceCollection();
        services.AddSingleton(replayService);
        var sut = new ReplayController(services.BuildServiceProvider());

        var result = sut.Advance();

        Assert.IsType<OkObjectResult>(result);
    }

    [Fact]
    public void Advance_WhenReplayServiceNotRegistered_ReturnsNotFound() {
        var services = new ServiceCollection();
        var sut = new ReplayController(services.BuildServiceProvider());

        var result = sut.Advance();

        Assert.IsType<NotFoundObjectResult>(result);
    }

    [Fact]
    public void Reset_WhenReplayServiceRegistered_ResetsAndReturnsOk() {
        var replayService = new ReplayCacheService([new EspnScores(), new EspnScores()]);
        replayService.Advance();
        var services = new ServiceCollection();
        services.AddSingleton(replayService);
        var sut = new ReplayController(services.BuildServiceProvider());

        var result = sut.Reset();

        Assert.IsType<OkObjectResult>(result);
    }

    [Fact]
    public void Reset_WhenReplayServiceNotRegistered_ReturnsNotFound() {
        var services = new ServiceCollection();
        var sut = new ReplayController(services.BuildServiceProvider());

        var result = sut.Reset();

        Assert.IsType<NotFoundObjectResult>(result);
    }
}
