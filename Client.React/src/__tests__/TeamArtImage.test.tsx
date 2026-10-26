import { act, fireEvent, render, screen } from '@testing-library/react';
import TeamArtImage from '../components/sports/TeamArtImage';

// One failed image request (a cellular blip, the home-screen app resuming mid-load) used to swap
// the logo for the text badge for the rest of the session. A failure is now retried first, and a
// logo that did give up comes back when the phone reconnects or the app returns to the foreground.
describe('TeamArtImage', () => {
  beforeEach(() => vi.useFakeTimers());
  afterEach(() => vi.useRealTimers());

  const renderLogo = (fallbackSrc?: string) =>
    render(<TeamArtImage abbr="PHI" src="/Icons/Logos/nfl/phi.png" fallbackSrc={fallbackSrc} size={50} showLabel={false} />);
  const img = () => screen.queryByRole('img', { name: 'PHI' }) as HTMLImageElement | null;
  const fail = () => fireEvent.error(img()!);
  const wait = (ms: number) => act(() => { vi.advanceTimersByTime(ms); });
  const badgeShown = () => img() === null || img()!.tagName !== 'IMG';

  it('retries a failed load instead of giving up on the first error', () => {
    renderLogo();
    fail();
    wait(5000);

    expect(img()?.tagName).toBe('IMG');
    expect(img()!.getAttribute('src')).toBe('/Icons/Logos/nfl/phi.png');
  });

  it('falls back to the text badge only after the retries are used up', () => {
    renderLogo();
    for (let i = 0; i < 3; i++) {
      fail();
      wait(5000);
    }

    expect(badgeShown()).toBe(true);
  });

  it('tries the fallback image before the badge', () => {
    renderLogo('/Icons/Logos/nfl/phi-light.png');
    for (let i = 0; i < 3; i++) {
      fail();
      wait(5000);
    }

    expect(img()!.getAttribute('src')).toBe('/Icons/Logos/nfl/phi-light.png');
  });

  it('tries again when the phone comes back online after giving up', () => {
    renderLogo();
    for (let i = 0; i < 3; i++) {
      fail();
      wait(5000);
    }
    expect(badgeShown()).toBe(true);

    act(() => { window.dispatchEvent(new Event('online')); });

    expect(img()?.tagName).toBe('IMG');
  });

  it('tries again when the app returns to the foreground after giving up', () => {
    renderLogo();
    for (let i = 0; i < 3; i++) {
      fail();
      wait(5000);
    }
    expect(badgeShown()).toBe(true);

    act(() => { document.dispatchEvent(new Event('visibilitychange')); });

    expect(img()?.tagName).toBe('IMG');
  });
});
