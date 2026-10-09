import { ACCUWEATHER_CONDITIONS, WEATHER_ICON_CLASS, mapWeatherFromEspn, toWeatherIconClass } from '../utils/weather';

describe('mapWeatherFromEspn', () => {
  it('returns "unknown" when displayValue is missing', () => {
    expect(mapWeatherFromEspn(null, null)).toBe('unknown');
    expect(mapWeatherFromEspn(undefined, undefined)).toBe('unknown');
  });

  it('maps known condition text to their weather keys', () => {
    expect(mapWeatherFromEspn('Thunderstorms', null)).toBe('thunderstorm');
    expect(mapWeatherFromEspn('Light Snow', null)).toBe('snow');
    expect(mapWeatherFromEspn('Heavy Rain', null)).toBe('rain-heavy');
    expect(mapWeatherFromEspn('Light Rain', null)).toBe('rain-light');
    expect(mapWeatherFromEspn('Showers', null)).toBe('rain');
    expect(mapWeatherFromEspn('Foggy', null)).toBe('fog');
    expect(mapWeatherFromEspn('Mostly Sunny', null)).toBe('mostly-clear');
    expect(mapWeatherFromEspn('Partly Cloudy', null)).toBe('partly-cloudy');
    expect(mapWeatherFromEspn('Overcast', null)).toBe('cloudy');
    expect(mapWeatherFromEspn('Clear', null)).toBe('clear');
    expect(mapWeatherFromEspn('Indoor', null)).toBe('indoor');
  });

  it('falls back to conditionId when displayValue is purely numeric', () => {
    expect(mapWeatherFromEspn('42', 'Sunny')).toBe('clear');
  });

  it('returns "unknown" for unrecognized text', () => {
    expect(mapWeatherFromEspn('Tornado Watch', null)).toBe('unknown');
  });

  // ESPN's conditionId is AccuWeather's icon number (1–44). Real payload, LV @ NE 2026-10-11:
  // { displayValue: "Hazy sunshine", conditionId: "5" } — previously rendered as N/A.
  it('maps the real "Hazy sunshine" payload to haze', () => {
    expect(mapWeatherFromEspn('Hazy sunshine', '5')).toBe('haze');
  });

  it('prefers the AccuWeather conditionId over the display text', () => {
    expect(mapWeatherFromEspn('Some new wording', '1')).toBe('clear');
    expect(mapWeatherFromEspn('Some new wording', '4')).toBe('partly-cloudy');
    expect(mapWeatherFromEspn('Some new wording', '8')).toBe('cloudy');
    expect(mapWeatherFromEspn('Some new wording', '11')).toBe('fog');
    expect(mapWeatherFromEspn('Some new wording', '15')).toBe('thunderstorm');
    expect(mapWeatherFromEspn('Some new wording', '18')).toBe('rain');
    expect(mapWeatherFromEspn('Some new wording', '22')).toBe('snow');
    expect(mapWeatherFromEspn('Some new wording', '25')).toBe('sleet');
    expect(mapWeatherFromEspn('Some new wording', '29')).toBe('rain-snow');
    expect(mapWeatherFromEspn('Some new wording', '30')).toBe('hot');
    expect(mapWeatherFromEspn('Some new wording', '31')).toBe('cold');
    expect(mapWeatherFromEspn('Some new wording', '32')).toBe('windy');
  });

  it('maps AccuWeather night codes to night variants', () => {
    expect(mapWeatherFromEspn('Clear', '33')).toBe('clear-night');
    expect(mapWeatherFromEspn('Mostly clear', '34')).toBe('mostly-clear-night');
    expect(mapWeatherFromEspn('Partly cloudy', '35')).toBe('partly-cloudy-night');
    expect(mapWeatherFromEspn('Hazy moonlight', '37')).toBe('haze-night');
    expect(mapWeatherFromEspn('Partly cloudy w/ showers', '39')).toBe('rain-light-night');
  });

  it('covers every AccuWeather code (1–44, minus the unused 9, 10, 27, 28)', () => {
    expect(Object.keys(ACCUWEATHER_CONDITIONS)).toHaveLength(40);
  });

  it('every AccuWeather code maps to a real icon, not wi-na', () => {
    for (const code of Object.keys(ACCUWEATHER_CONDITIONS)) {
      expect(toWeatherIconClass(mapWeatherFromEspn('x', code)), `code ${code}`).not.toBe('wi-na');
    }
  });

  it('falls back to text matching for an unrecognized conditionId', () => {
    expect(mapWeatherFromEspn('Light Rain', '999')).toBe('rain-light');
  });

  it('text fallback recognizes "hazy" and "sunshine" wording', () => {
    expect(mapWeatherFromEspn('Hazy sunshine', null)).toBe('haze');
    expect(mapWeatherFromEspn('Sunshine', null)).toBe('clear');
  });

  // Text fallback agrees with the code table (CFB DB rows can carry text with a null conditionId).
  it('text fallback reaches the same keys as the AccuWeather codes', () => {
    expect(mapWeatherFromEspn('Sleet', null)).toBe('sleet');
    expect(mapWeatherFromEspn('Freezing rain', null)).toBe('sleet');
    expect(mapWeatherFromEspn('Rain and snow', null)).toBe('rain-snow');
    expect(mapWeatherFromEspn('Hot', null)).toBe('hot');
    expect(mapWeatherFromEspn('Cold', null)).toBe('cold');
    expect(mapWeatherFromEspn('Windy', null)).toBe('windy');
    expect(mapWeatherFromEspn('Dreary', null)).toBe('cloudy');
  });

  it('an "Indoor" displayValue wins over a conditionId', () => {
    expect(mapWeatherFromEspn('Indoor', '18')).toBe('indoor');
  });

  it('night storm and snow codes use night icons', () => {
    expect(toWeatherIconClass(mapWeatherFromEspn('x', '41'))).toBe('wi-night-thunderstorm');
    expect(toWeatherIconClass(mapWeatherFromEspn('x', '42'))).toBe('wi-night-thunderstorm');
    expect(toWeatherIconClass(mapWeatherFromEspn('x', '43'))).toBe('wi-night-snow');
    expect(toWeatherIconClass(mapWeatherFromEspn('x', '44'))).toBe('wi-night-snow');
  });
});

// Non-eager glob: only the matched file paths are needed, not their contents.
const iconFiles = new Set(
  Object.keys(import.meta.glob('../../public/Icons/Weather/svg/*.svg')).map(p => p.split('/').pop()),
);

describe('toWeatherIconClass', () => {
  it('maps the real-payload keys to the expected icons', () => {
    expect(toWeatherIconClass('haze')).toBe('wi-day-haze');
    expect(toWeatherIconClass('partly-cloudy')).toBe('wi-day-cloudy');
    expect(toWeatherIconClass('haze-night')).toBe('wi-night-fog');
  });

  it('every icon class in the map exists in public/Icons/Weather/svg', () => {
    for (const [key, iconClass] of Object.entries(WEATHER_ICON_CLASS)) {
      expect(iconFiles.has(`${iconClass}.svg`), `${key} → ${iconClass}.svg`).toBe(true);
    }
    expect(iconFiles.has('wi-na.svg')).toBe(true);
  });

  it('falls back to "wi-na" for an unknown, indoor, or prototype-name key', () => {
    expect(toWeatherIconClass('unknown')).toBe('wi-na');
    expect(toWeatherIconClass('indoor')).toBe('wi-na');
    expect(toWeatherIconClass('toString')).toBe('wi-na');
  });
});
