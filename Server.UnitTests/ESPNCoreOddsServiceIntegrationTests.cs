using FourPlayWebApp.Server.Services;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace FourPlayWebApp.Server.UnitTests;

public class EspnCoreOddsServiceLiveApiTests {
    //[Fact(Skip = "Live integration test. Run manually.")]
    [Fact]
    public async Task GetEventsWithOddsAsync_ReturnsRealEventData() {
        // Arrange
        var httpClientBase = new HttpClient {
            BaseAddress = new Uri("http://site.api.espn.com")
        };
        var baseService = new EspnApiService(httpClientBase,
            new LoggerFactory().CreateLogger<EspnApiService>());
        var setEvents = await baseService.GetWeekScores(10, 2024);
        Assert.NotNull(setEvents);
        Assert.NotEmpty(setEvents.Events);
        Assert.Equal("401671810", setEvents.Events.First().Id);
        // Arrange
        var httpClient = new HttpClient {
            BaseAddress = new Uri("https://sports.core.api.espn.com")
        };

        var service = new EspnCoreOddsService(httpClient);

        // Act
        var events = await service.GetEventsWithOddsAsync(int.Parse(setEvents.Events.First().Id));

        // Assert
        Assert.NotEmpty(events.Items);
        // Act
        var oddsEvents = await service.GetEventsWithOddsAsync(int.Parse(setEvents.Events.First().Id), 58); //ESPN BET

        // Assert
        Assert.NotEmpty(oddsEvents.Details);
        // Act
        oddsEvents = await service.GetEventsWithOddsAsync(int.Parse(setEvents.Events.First().Id), -1); //Not Real

        // Assert
        Assert.Null(oddsEvents);
    }
}
