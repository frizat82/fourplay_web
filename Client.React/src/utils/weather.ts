// Semantic weather key → local Erik Flowers icon (public/Icons/Weather/svg/<class>.svg).
// Anything not listed (including 'indoor' and 'unknown') renders as wi-na.
export const WEATHER_ICON_CLASS = {
  'clear': 'wi-day-sunny',
  'mostly-clear': 'wi-day-sunny-overcast',
  'partly-cloudy': 'wi-day-cloudy',
  'cloudy': 'wi-cloudy',
  'rain-light': 'wi-day-rain',
  'rain': 'wi-rain',
  'rain-heavy': 'wi-showers',
  'thunderstorm': 'wi-thunderstorm',
  'snow': 'wi-snow',
  'sleet': 'wi-sleet',
  'rain-snow': 'wi-rain-mix',
  'fog': 'wi-fog',
  'haze': 'wi-day-haze',
  'hot': 'wi-hot',
  'cold': 'wi-snowflake-cold',
  'windy': 'wi-strong-wind',
  'clear-night': 'wi-night-clear',
  'mostly-clear-night': 'wi-night-partly-cloudy',
  'partly-cloudy-night': 'wi-night-cloudy',
  'haze-night': 'wi-night-fog',
  'rain-light-night': 'wi-night-showers',
  'thunderstorm-night': 'wi-night-thunderstorm',
  'snow-night': 'wi-night-snow',
} as const;

type WeatherIconKey = keyof typeof WEATHER_ICON_CLASS;

function isWeatherIconKey(key: string): key is WeatherIconKey {
  return Object.prototype.hasOwnProperty.call(WEATHER_ICON_CLASS, key);
}

// ESPN's weather.conditionId is AccuWeather's icon number (ESPN links weather to accuweather.com).
// The code is stable where the display text isn't ("Hazy sunshine", "Intermittent clouds", "Dreary"…),
// so it's the primary source; text matching is only a fallback for missing/unknown codes.
// Codes 9, 10, 27, 28 are unused by AccuWeather.
export const ACCUWEATHER_CONDITIONS: Record<number, WeatherIconKey> = {
  1: 'clear', 2: 'mostly-clear', 3: 'mostly-clear', 4: 'partly-cloudy', 5: 'haze',
  6: 'cloudy', 7: 'cloudy', 8: 'cloudy', 11: 'fog',
  12: 'rain', 13: 'rain', 14: 'rain-light',
  15: 'thunderstorm', 16: 'thunderstorm', 17: 'thunderstorm',
  18: 'rain', 19: 'snow', 20: 'snow', 21: 'snow', 22: 'snow', 23: 'snow',
  24: 'sleet', 25: 'sleet', 26: 'sleet', 29: 'rain-snow',
  30: 'hot', 31: 'cold', 32: 'windy',
  33: 'clear-night', 34: 'mostly-clear-night', 35: 'partly-cloudy-night', 36: 'partly-cloudy-night',
  37: 'haze-night', 38: 'cloudy', 39: 'rain-light-night', 40: 'rain',
  41: 'thunderstorm-night', 42: 'thunderstorm-night', 43: 'snow-night', 44: 'snow-night',
};

export function mapWeatherFromEspn(
  displayValue?: string | null,
  conditionId?: string | null,
): WeatherIconKey | 'indoor' | 'unknown' {
  if (!displayValue) return 'unknown';
  // A dome game's forecast code is the city's weather, not the field's.
  if (displayValue.toLowerCase().includes('indoor')) return 'indoor';
  const fromCode = conditionId ? ACCUWEATHER_CONDITIONS[Number(conditionId)] : undefined;
  if (fromCode) return fromCode;

  let value = displayValue;
  if (conditionId && Number.isFinite(Number(displayValue))) {
    value = conditionId;
  }
  const disp = value.toLowerCase();

  if (disp.includes('thunder') || disp.includes('storm')) return 'thunderstorm';
  if (disp.includes('rain and snow')) return 'rain-snow';
  if (disp.includes('sleet') || disp.includes('freezing')) return 'sleet';
  if (disp.includes('snow') || disp.includes('flurr')) return 'snow';
  if (disp.includes('heavy rain') || disp.includes('downpour') || disp.includes('torrential')) return 'rain-heavy';
  if (disp.includes('drizzle') || disp.includes('light rain') || disp.includes('sprinkle')) return 'rain-light';
  if (disp.includes('rain') || disp.includes('shower')) return 'rain';
  if (disp.includes('fog') || disp.includes('mist')) return 'fog';
  if (disp.includes('haz')) return 'haze';
  if (disp.includes('mostly clear') || disp.includes('mostly sunny') || disp.includes('partly sunny')) return 'mostly-clear';
  if (disp.includes('partly') || disp.includes('few clouds')) return 'partly-cloudy';
  if (disp.includes('cloud') || disp.includes('overcast') || disp.includes('dreary')) return 'cloudy';
  if (disp.includes('clear') || disp.includes('sunny') || disp.includes('sunshine')) return 'clear';
  if (disp.includes('wind')) return 'windy';
  if (disp.includes('hot')) return 'hot';
  if (disp.includes('cold')) return 'cold';
  return 'unknown';
}

export function toWeatherIconClass(key: string) {
  return isWeatherIconKey(key) ? WEATHER_ICON_CLASS[key] : 'wi-na';
}
