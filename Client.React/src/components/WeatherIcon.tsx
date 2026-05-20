import { Stack, Typography } from '@mui/material';
import { useTheme } from '@mui/material/styles';
import { mapWeatherFromEspn, toWeatherIconClass } from '../utils/weather';

interface WeatherIconProps {
  iconKey?: string | null;
  conditionId?: string | null;
  temperatureF?: number | null;
  showTemp?: boolean;
}

export default function WeatherIcon({ iconKey, conditionId, temperatureF, showTemp = true }: WeatherIconProps) {
  const theme = useTheme();
  if (!iconKey) return null;

  const semanticKey = mapWeatherFromEspn(iconKey, conditionId);
  const iconClass = toWeatherIconClass(semanticKey);
  const iconSrc = `/Icons/Weather/svg/${iconClass}.svg`;
  const altText = semanticKey.replace('-', ' ');
  const iconFilter = theme.palette.mode === 'dark' ? 'brightness(0) invert(1)' : 'brightness(0)';

  return (
    <Stack direction="row" alignItems="center" spacing={1}>
      <img src={iconSrc} alt={altText} style={{ width: 32, height: 32, filter: iconFilter }} />
      {showTemp && typeof temperatureF === 'number' && (
        <Typography variant="body2" sx={{ opacity: 0.9 }}>
          {temperatureF}°
        </Typography>
      )}
    </Stack>
  );
}
