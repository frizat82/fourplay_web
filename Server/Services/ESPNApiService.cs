using FourPlayWebApp.Server.Models.Data;
using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;
using Serilog;
using FourPlayWebApp.Server.Services.Interfaces;
using FourPlayWebApp.Shared.Helpers;
using FourPlayWebApp.Shared.Helpers.Extensions;
using FourPlayWebApp.Shared.Models;
using ILogger=Microsoft.Extensions.Logging.ILogger;

namespace FourPlayWebApp.Server.Services;

public class EspnApiService(HttpClient httpClient, ILogger<EspnApiService> logger)
    : IEspnApiService {
    private const string _scoreboardEndpoint = "/apis/site/v2/sports/football/nfl/scoreboard";
    public async Task<EspnScores?> GetWeekScores(int week, int year, bool postSeason = false) {
        try {
            // Replace the endpoint with the actual ESPN API endpoint for NFL spreads
            var response = await httpClient.GetAsync(
            $"{_scoreboardEndpoint}?dates={year}&seasontype={(postSeason ? 3 : 2)}&week={week}");
            response.EnsureSuccessStatusCode();
            if (response.IsSuccessStatusCode) {
                var responseString = await response.Content.ReadAsStringAsync();
                var options = new JsonSerializerOptions {
                    PropertyNameCaseInsensitive = true
                };
                EspnApiServiceJsonConverter.Settings.Converters.ToList().ForEach(x => options.Converters.Add(x));
                // Fix some strange team abbreviations from ESPN that don't match standard ones
                foreach (var map in NflTeamMappingHelpers.NflTeamAbbrMapping.Where(map =>
                             responseString.Contains($"\"{map.Key}\""))) {
                    responseString = responseString.Replace($"\"{map.Key}\"", $"\"{map.Value}\"");
                }

                var deserializedObject = JsonSerializer.Deserialize<EspnScores>(responseString, options);

                // Use the deserialized object as needed
                return FixEspnProbBowlWeek(deserializedObject);
            }

            logger.LogError("Error: {ResponseReasonPhrase}", response.ReasonPhrase);
            return null;
        }
        catch (HttpRequestException e) {
            logger.LogError("HTTP Request error: {EMessage}", e.Message);
            return null;
        }
    }
    public async Task<EspnScores?> GetSeasonScores(int year) {
        try {
            // Replace the endpoint with the actual ESPN API endpoint for NFL spreads
            var response = await httpClient.GetAsync(
            $"{_scoreboardEndpoint}?dates={year}&limit=1000");
            response.EnsureSuccessStatusCode();
            if (response.IsSuccessStatusCode) {
                var responseString = await response.Content.ReadAsStringAsync();
                var options = new JsonSerializerOptions {
                    PropertyNameCaseInsensitive = true
                };
                EspnApiServiceJsonConverter.Settings.Converters.ToList().ForEach(x => options.Converters.Add(x));
                // Fix some strange team abbreviations from ESPN that don't match standard ones
                foreach (var map in NflTeamMappingHelpers.NflTeamAbbrMapping.Where(map =>
                             responseString.Contains($"\"{map.Key}\""))) {
                    responseString = responseString.Replace($"\"{map.Key}\"", $"\"{map.Value}\"");
                }

                var deserializedObject = JsonSerializer.Deserialize<EspnScores>(responseString, options);

                // Use the deserialized object as needed
                return FixEspnProbBowlWeek(deserializedObject);
            }

            logger.LogError("Error: {ResponseReasonPhrase}", response.ReasonPhrase);
            return null;
        }
        catch (HttpRequestException e) {
            logger.LogError("HTTP Request error: {EMessage}", e.Message);
            return null;
        }
    }

    public async Task<EspnScores?> GetScores() {
        try {
            // Replace the endpoint with the actual ESPN API endpoint for NFL spreads
            var response = await httpClient.GetAsync(_scoreboardEndpoint);
            response.EnsureSuccessStatusCode();
            if (response.IsSuccessStatusCode) {
                var responseString = await response.Content.ReadAsStringAsync();
                if (string.IsNullOrEmpty(responseString))
                    return null;
                var options = new JsonSerializerOptions {
                    PropertyNameCaseInsensitive = true
                };
                EspnApiServiceJsonConverter.Settings.Converters.ToList()
                    .ForEach(x => options.Converters.Add(x));
                foreach (var map in NflTeamMappingHelpers.NflTeamAbbrMapping) {
                    if (responseString.Contains($"\"{map.Key}\""))
                        responseString = responseString.Replace($"\"{map.Key}\"", $"\"{map.Value}\"");
                }

                // Deserialize the JSON response into a .NET object
                var deserializedObject = JsonSerializer.Deserialize<EspnScores>(responseString, options);


                // Use the deserialized object as needed
                logger.LogDebug("Deserialized object: {DeserializedObject}", deserializedObject);
                // Use the deserialized object as needed
                return FixEspnProbBowlWeek(deserializedObject);
            }
            logger.LogWarning("Error: {ResponseReasonPhrase}", response.ReasonPhrase);
        }
        catch (HttpRequestException e) {
            logger.LogError("HTTP Request error: {EMessage}", e.Message);
        }

        return null;
    }

    private EspnScores? FixEspnProbBowlWeek(EspnScores? scores) {
        if (scores == null || !scores.Events.Any())
            return scores;

        // Remove teams with Abbr "NFC" or "AFC" and fix Pro Bowl
        foreach (var scoreEvent in scores.Events) {
            scoreEvent.Competitions = scoreEvent.Competitions
                .Where(c => !c.Competitors.Any(team => team.Team.Abbreviation == "NFC" || team.Team.Abbreviation == "AFC"))
                .ToArray();
        }

        // Move back Super Bowl to a proper week
        if (scores.IsPostSeason() && scores.Week.Number == 5) {
            scores.Week.Number = 4;
        }
        // Update SeasonType and Week number
        foreach (var scoreEvent in scores.Events) {
            if (scoreEvent.IsPostSeason() && scoreEvent.Week.Number == 5) {
                scoreEvent.Week.Number = 4;
            }
        }

        return scores;
    }
}
