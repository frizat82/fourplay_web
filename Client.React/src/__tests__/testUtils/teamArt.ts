import { act, fireEvent, screen } from '@testing-library/react';
import { RETRY_DELAYS_MS, resetTeamArtFailures } from '../../components/sports/TeamArtImage';

// The <img> for a team (the text badge shares its role and name, so pick the real image).
export const teamImg = (name: string) =>
  screen.queryAllByRole('img', { name }).find((e): e is HTMLImageElement => e.tagName === 'IMG');

// What the HEAD check after the last retry finds: the file really missing (Vercel answers a missing
// path with its HTML page) or present (so the failed loads were a network interruption).
export function stubImageHead(found: 'missing' | 'present') {
  vi.stubGlobal('fetch', vi.fn(async () => found === 'missing'
    ? new Response('<!doctype html>', { status: 200, headers: { 'content-type': 'text/html' } })
    : new Response(null, { status: 200, headers: { 'content-type': 'image/png' } })));
}

const flush = () => act(async () => { await Promise.resolve(); await Promise.resolve(); });

// Fails the team's current image through every retry, then lets the HEAD check settle (fake timers on).
export async function failForGood(name: string) {
  for (let i = 0; i <= RETRY_DELAYS_MS.length; i++) {
    fireEvent.error(teamImg(name)!);
    await act(async () => { vi.advanceTimersByTime(Math.max(...RETRY_DELAYS_MS)); });
  }
  await flush();
}

export function resetTeamArt() {
  resetTeamArtFailures();
  vi.unstubAllGlobals();
}
