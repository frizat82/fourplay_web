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

  // Dome games come back from ESPN as displayValue="Indoor" — a stadium icon reads better there
  // than N/A, and every other card still has an icon in that spot.
  it('renders a stadium icon for an indoor game (dome stadium)', () => {
    const { container } = render(<WeatherIcon iconKey="Indoor" />);
    expect(container.firstChild).not.toBeNull();
    expect(container.querySelector('svg')).not.toBeNull();
  });

  it('keeps the N/A icon for an unrecognized condition', () => {
    const { container } = render(<WeatherIcon iconKey="Tornado Watch" />);
    expect(container.querySelector('img')?.getAttribute('src')).toContain('wi-na.svg');
  });
});
