// ESPNCoreOddsService.cs

using FourPlayWebApp.Server.Services.Interfaces;
using FourPlayWebApp.Shared.Models;
using Serilog;
using System.Text.Json;

namespace FourPlayWebApp.Server.Services;

public class EspnCoreOddsService(HttpClient httpClient) : IEspnCoreOddsService {
    private readonly JsonSerializerOptions _options = new() {PropertyNameCaseInsensitive = true};

    public async Task<EspnCoreOddsApiResponse?> GetEventsWithOddsAsync(int eventId) {
        try {
            var response =
                await httpClient.GetStringAsync(
                    $"/v2/sports/football/leagues/nfl/events/{eventId}/competitions/{eventId}/odds");
            var data = JsonSerializer.Deserialize<EspnCoreOddsApiResponse>(response, _options);
            return data;
        }
        catch (Exception e) {
            Log.Error(e, "Error fetching odds for event {EventId}", eventId);
            return null;
        }
    }

    public async Task<EspnCoreOddsItem?> GetEventsWithOddsAsync(int eventId, int providerId)
    {
        try {
            var response = await httpClient.GetStringAsync(
                $"/v2/sports/football/leagues/nfl/events/{eventId}/competitions/{eventId}/odds/{providerId}");
            var data = JsonSerializer.Deserialize<EspnCoreOddsItem>(response, _options);
            return data;
        } catch (Exception e) {
            Log.Error(e, "Error fetching odds for event {EventId} with provider {ProviderId}", eventId, providerId);
            return null;
        }
    }
}
