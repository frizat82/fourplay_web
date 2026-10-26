import { act, fireEvent, render } from '@testing-library/react';
import TeamArtImage, { RETRY_DELAYS_MS } from '../components/sports/TeamArtImage';
import { failForGood, resetTeamArt, stubImageHead, teamImg } from './testUtils/teamArt';

// One failed image request (a cellular blip, the home-screen app resuming mid-load) used to swap
// the logo for the text badge for the rest of the session. Now a failure is retried; a src that
// gives up is checked once — really missing (skipped from then on) or interrupted (tried again when
// the phone is back online or the app returns to the foreground).
describe('TeamArtImage', () => {
  beforeEach(() => vi.useFakeTimers());
  afterEach(() => { vi.useRealTimers(); resetTeamArt(); });

  const PHI = '/Icons/Logos/nfl/phi.png';
  const logo = (src = PHI, fallbackSrc?: string) =>
    <TeamArtImage abbr="PHI" src={src} fallbackSrc={fallbackSrc} size={50} showLabel={false} />;
  const wait = (ms: number) => act(() => { vi.advanceTimersByTime(ms); });

  it('shows the badge, not a broken image, while waiting to retry, then retries the same src', () => {
    render(logo());
    fireEvent.error(teamImg('PHI')!);
    expect(teamImg('PHI')).toBeUndefined();

    wait(RETRY_DELAYS_MS[0]);

    expect(teamImg('PHI')?.getAttribute('src')).toBe(PHI);
  });

  it('a file that is really missing is given up on, and later screens skip it without requesting it', async () => {
    stubImageHead('missing');
    const first = render(logo());
    await failForGood('PHI');
    expect(teamImg('PHI')).toBeUndefined();
    first.unmount();

    render(logo());

    expect(teamImg('PHI')).toBeUndefined();
  });

  it('a missing src moves on to the fallback image', async () => {
    stubImageHead('missing');
    render(logo('/Icons/Logos/nfl-dark/phi.png', PHI));

    await failForGood('PHI');

    expect(teamImg('PHI')?.getAttribute('src')).toBe(PHI);
  });

  it('a missing file is not re-requested when the app returns to the foreground', async () => {
    stubImageHead('missing');
    render(logo());
    await failForGood('PHI');

    act(() => { window.dispatchEvent(new Event('visibilitychange')); });

    expect(teamImg('PHI')).toBeUndefined();
  });

  it('an interrupted load is tried again when the phone comes back online', async () => {
    stubImageHead('present');
    render(logo());
    await failForGood('PHI');
    expect(teamImg('PHI')).toBeUndefined();

    act(() => {
      window.dispatchEvent(new Event('offline'));
      window.dispatchEvent(new Event('online'));
    });

    expect(teamImg('PHI')?.getAttribute('src')).toBe(PHI);
  });

  it('an interrupted load is tried again when the app returns to the foreground', async () => {
    stubImageHead('present');
    render(logo());
    await failForGood('PHI');

    act(() => { window.dispatchEvent(new Event('visibilitychange')); });

    expect(teamImg('PHI')?.getAttribute('src')).toBe(PHI);
  });

  it('a new src (theme switch) mid-retry starts with its own retries', () => {
    const fetchSpy = vi.fn();
    vi.stubGlobal('fetch', fetchSpy);
    const { rerender } = render(logo());
    fireEvent.error(teamImg('PHI')!);

    rerender(logo('/Icons/Logos/nfl-dark/phi.png'));
    wait(Math.max(...RETRY_DELAYS_MS));
    fireEvent.error(teamImg('PHI')!);

    expect(teamImg('PHI')).toBeUndefined(); // waiting to retry, not given up
    expect(fetchSpy).not.toHaveBeenCalled();
  });

  it('a pending retry does nothing after unmount', () => {
    const fetchSpy = vi.fn();
    vi.stubGlobal('fetch', fetchSpy);
    const { unmount } = render(logo());
    fireEvent.error(teamImg('PHI')!);
    unmount();

    wait(Math.max(...RETRY_DELAYS_MS) * 3);

    expect(fetchSpy).not.toHaveBeenCalled();
  });
});
