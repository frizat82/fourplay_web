import { mapWeatherFromEspn, toWeatherIconClass } from '../utils/weather';

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
});

describe('toWeatherIconClass', () => {
  it('maps each known weather key to its icon class', () => {
    expect(toWeatherIconClass('clear')).toBe('wi-day-sunny');
    expect(toWeatherIconClass('mostly-clear')).toBe('wi-day-sunny-overcast');
    expect(toWeatherIconClass('partly-cloudy')).toBe('wi-day-cloudy');
    expect(toWeatherIconClass('cloudy')).toBe('wi-cloudy');
    expect(toWeatherIconClass('rain-light')).toBe('wi-day-rain');
    expect(toWeatherIconClass('rain')).toBe('wi-rain');
    expect(toWeatherIconClass('rain-heavy')).toBe('wi-showers');
    expect(toWeatherIconClass('thunderstorm')).toBe('wi-thunderstorm');
    expect(toWeatherIconClass('snow')).toBe('wi-snow');
    expect(toWeatherIconClass('fog')).toBe('wi-fog');
    expect(toWeatherIconClass('indoor')).toBe('wi-na');
  });

  it('falls back to "wi-na" for an unknown key', () => {
    expect(toWeatherIconClass('unknown')).toBe('wi-na');
  });
});
