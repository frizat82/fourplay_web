import { render } from '@testing-library/react';
import WeatherIcon from '../components/WeatherIcon';

vi.mock('@mui/material/styles', () => ({
  useTheme: () => ({ palette: { mode: 'light' } }),
}));

describe('WeatherIcon', () => {
  it('renders nothing when iconKey is null', () => {
    const { container } = render(<WeatherIcon iconKey={null} />);
    expect(container.firstChild).toBeNull();
  });

  it('renders a weather icon for a normal outdoor condition', () => {
    const { container } = render(<WeatherIcon iconKey="Partly Cloudy" conditionId="4" />);
    const img = container.querySelector('img');
    expect(img).not.toBeNull();
    expect(img?.src).toContain('wi-day-cloudy.svg');
  });

  // frizat: Cowboys home games at AT&T Stadium (dome) come back from ESPN as
  // displayValue="Indoor". Previously this rendered wi-na.svg — a generic N/A
  // icon that looked different from every other game card's weather icon.
  it('renders a stadium icon for an indoor game (dome stadium)', () => {
    const { container } = render(<WeatherIcon iconKey="Indoor" />);
    expect(container.firstChild).not.toBeNull();
    expect(container.querySelector('svg')).not.toBeNull();
  });

  it('renders nothing for an unknown weather condition', () => {
    const { container } = render(<WeatherIcon iconKey="Tornado Watch" />);
    expect(container.firstChild).toBeNull();
  });
});
