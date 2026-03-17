using FourPlayWebApp.Server.Services;
using FourPlayWebApp.Shared.Models;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Serilog.Extensions.Logging;
using System.Net;

namespace FourPlayWebApp.Server.UnitTests {
    public class EspnApiServiceIntegrationTests {
        [Fact]
        public async Task GetSportsDataAsync_ReturnsValidSportsData() {
            // Arrange
            var httpClient = new HttpClient {
                BaseAddress = new Uri("http://site.api.espn.com")
            };

            var baseService = new EspnApiService(httpClient, new LoggerFactory().CreateLogger<EspnApiService>());

            // Act
            var sportsData = await baseService.GetWeekScores(1, 2025);
            var activeScores = await baseService.GetScores();
            // Assert
            Assert.NotNull(sportsData);
            Assert.NotEmpty(sportsData.Events);
            Assert.NotEmpty(sportsData.Leagues.First().Name);

            // Additional validation
            var firstSport = sportsData.Events.First();
            var activeSport =
                activeScores.Events.SelectMany(x => x.Competitions.Where(x => !x.Status.Type.Completed));
            Assert.NotNull(firstSport);
            Assert.False(string.IsNullOrWhiteSpace(firstSport.Id));
            var competition = firstSport.Competitions.First();
            Assert.False(
                string.IsNullOrWhiteSpace(competition.Competitors.First().Team.Abbreviation));
            //var situation = activeSport.First().Situation;
            //Assert.NotEmpty(situation.DownDistanceText);
            var firstTeam = competition.Competitors.First();
            Assert.NotEmpty(firstTeam.Records);
            var records = firstTeam.Records.First();
            Assert.NotEmpty(records.Summary);

        }


        [Fact(Skip = "Used to grab Icons")]
        //[Fact]
        public async Task GrabIcons() {
            // Arrange
            var httpClient = new HttpClient {
                BaseAddress = new Uri("http://site.api.espn.com")
            };

            var baseService = new EspnApiService(httpClient, new LoggerFactory().CreateLogger<EspnApiService>());

            // Act
            var sportsData = await baseService.GetWeekScores(1, 2025);

            foreach (var events in sportsData.Events)
            foreach (var sportEvent in events.Competitions) {
                foreach (var team in sportEvent.Competitors) {
                    using var client = new HttpClient();
                    await using var stream = await client.GetStreamAsync(team.Team.Logo);
                    await using var fileStream = new FileStream(Path.Combine(".",team.Team.Abbreviation.ToLower() + ".png"), FileMode.Create);
                    await stream.CopyToAsync(fileStream);
                    await fileStream.FlushAsync();
                }
            }
        }

    }
}