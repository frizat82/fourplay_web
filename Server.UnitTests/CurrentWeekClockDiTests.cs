using FourPlayWebApp.Server.Services;
using FourPlayWebApp.Server.Services.Repositories.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Xunit;

namespace FourPlayWebApp.Server.UnitTests;

// frizat-tf2 /code-review: every other test for NflCurrentWeekService/CfbCurrentSlateService
// constructs them manually with an explicit TimeProvider, bypassing the actual DI wiring in
// Program.cs entirely — nothing proves [FromKeyedServices(CurrentWeekClock.Key)] on their
// constructors actually resolves against the AddKeyedSingleton<TimeProvider> registration rather
// than throwing or silently binding to the wrong provider. This builds a real ServiceProvider
// with just that registration to prove the wiring itself is correct.
public class CurrentWeekClockDiTests {
    [Fact]
    public void KeyedTimeProvider_ResolvesForBothServices_ThroughRealDiContainer() {
        var services = new ServiceCollection();
        services.AddKeyedSingleton<TimeProvider>(CurrentWeekClock.Key, TimeProvider.System);
        services.AddSingleton(Substitute.For<ILeagueRepository>());
        services.AddSingleton(Substitute.For<ICfbRepository>());
        services.AddSingleton<NflCurrentWeekService>();
        services.AddSingleton<CfbCurrentSlateService>();
        var provider = services.BuildServiceProvider();

        Assert.NotNull(provider.GetRequiredService<NflCurrentWeekService>());
        Assert.NotNull(provider.GetRequiredService<CfbCurrentSlateService>());
    }
}
