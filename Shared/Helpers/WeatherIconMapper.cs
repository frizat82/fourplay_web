using FourPlayWebApp.Shared.Models;

namespace FourPlayWebApp.Shared.Helpers
{
    // Map ESPN weather textual descriptions to erikflowers weather-icons keys
    public static class WeatherIconMapper
    {
        // Keys correspond to erikflowers icon class suffixes (e.g., wi-day-sunny, wi-cloudy, wi-rain)
        // We'll return a semantic key that the client component can map to the CSS class or sprite.

        public static string MapFromEspn(string? displayValue, string? conditionId = null)
        {
            if (string.IsNullOrWhiteSpace(displayValue))
                return "unknown";
            // fallback to condition id if provided (some ESPN data may include numeric ids similar to OpenWeather)
            if (!string.IsNullOrWhiteSpace(conditionId) && int.TryParse(displayValue, out var id)) {
                displayValue = conditionId;
            }

            var disp = displayValue.ToLowerInvariant();

            if (disp.Contains("thunder") || disp.Contains("storm")) return "thunderstorm";
            if (disp.Contains("snow") || disp.Contains("sleet") || disp.Contains("flurr")) return "snow";
            if (disp.Contains("freezing")) return "snow";
            if (disp.Contains("heavy rain") || disp.Contains("downpour") || disp.Contains("torrential")) return "rain-heavy";
            if (disp.Contains("drizzle") || disp.Contains("light rain") || disp.Contains("sprinkle")) return "rain-light";
            if (disp.Contains("rain") || disp.Contains("shower")) return "rain";
            if (disp.Contains("fog") || disp.Contains("mist") || disp.Contains("haze")) return "fog";
            if (disp.Contains("mostly clear") || disp.Contains("mostly sunny") || disp.Contains("partly sunny")) return "mostly-clear";
            if (disp.Contains("partly") || disp.Contains("few clouds")) return "partly-cloudy";
            if (disp.Contains("cloud") || disp.Contains("overcast")) return "cloudy";
            if (disp.Contains("clear") || disp.Contains("sunny")) return "clear";
            if (disp.Contains("indoor")) return "indoor";

            return "unknown";
        }

        // Map our semantic key to the erikflowers CSS class name
        public static string ToErikFlowersClass(string semanticKey)
        {
            return semanticKey switch
            {
                "clear" => "wi-day-sunny",
                "mostly-clear" => "wi-day-sunny-overcast",
                "partly-cloudy" => "wi-day-cloudy",
                "cloudy" => "wi-cloudy",
                "rain-light" => "wi-day-rain",
                "rain" => "wi-rain",
                "rain-heavy" => "wi-showers",
                "thunderstorm" => "wi-thunderstorm",
                "snow" => "wi-snow",
                "fog" => "wi-fog",
                "indoor" => "wi-na",
                _ => "wi-na",
            };
        }
    }
}

