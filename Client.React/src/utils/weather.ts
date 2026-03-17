export function mapWeatherFromEspn(displayValue?: string | null, conditionId?: string | null) {
  if (!displayValue) return 'unknown';
  let value = displayValue;
  if (conditionId && Number.isFinite(Number(displayValue))) {
    value = conditionId;
  }
  const disp = value.toLowerCase();

  if (disp.includes('thunder') || disp.includes('storm')) return 'thunderstorm';
  if (disp.includes('snow') || disp.includes('sleet') || disp.includes('flurr')) return 'snow';
  if (disp.includes('freezing')) return 'snow';
  if (disp.includes('heavy rain') || disp.includes('downpour') || disp.includes('torrential')) return 'rain-heavy';
  if (disp.includes('drizzle') || disp.includes('light rain') || disp.includes('sprinkle')) return 'rain-light';
  if (disp.includes('rain') || disp.includes('shower')) return 'rain';
  if (disp.includes('fog') || disp.includes('mist') || disp.includes('haze')) return 'fog';
  if (disp.includes('mostly clear') || disp.includes('mostly sunny') || disp.includes('partly sunny')) return 'mostly-clear';
  if (disp.includes('partly') || disp.includes('few clouds')) return 'partly-cloudy';
  if (disp.includes('cloud') || disp.includes('overcast')) return 'cloudy';
  if (disp.includes('clear') || disp.includes('sunny')) return 'clear';
  if (disp.includes('indoor')) return 'indoor';
  return 'unknown';
}

export function toWeatherIconClass(key: string) {
  switch (key) {
    case 'clear':
      return 'wi-day-sunny';
    case 'mostly-clear':
      return 'wi-day-sunny-overcast';
    case 'partly-cloudy':
      return 'wi-day-cloudy';
    case 'cloudy':
      return 'wi-cloudy';
    case 'rain-light':
      return 'wi-day-rain';
    case 'rain':
      return 'wi-rain';
    case 'rain-heavy':
      return 'wi-showers';
    case 'thunderstorm':
      return 'wi-thunderstorm';
    case 'snow':
      return 'wi-snow';
    case 'fog':
      return 'wi-fog';
    case 'indoor':
      return 'wi-na';
    default:
      return 'wi-na';
  }
}
