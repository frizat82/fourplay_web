import { useEffect, useRef, useState, useSyncExternalStore } from 'react';
import { focusManager, onlineManager } from '@tanstack/react-query';
import TeamArtFrame from './TeamArtFrame';
import TeamFallbackBadge from './TeamFallbackBadge';

interface TeamArtImageProps {
  abbr: string;
  src: string;
  // Tried if src fails to load, before giving up to the text badge.
  fallbackSrc?: string;
  size: number;
  height?: number;
  showLabel: boolean;
  cacheKey?: string;
}

// A failed load is usually transient (a cellular blip, the home-screen app resuming mid-load), so
// each src is retried after these delays before moving on to the fallback src, then the badge.
export const RETRY_DELAYS_MS = [1000, 4000];

// Why a src gave up, shared by every TeamArtImage (the same team appears on cards, the picks matrix
// and the leaderboard). 'missing': the file isn't there (404, or Vercel's SPA fallback page) — skipped
// for the rest of the session. 'interrupted': the network failed — tried again once the phone is back
// online or the app returns to the foreground (TanStack Query's managers own those listeners).
type Failure = 'missing' | 'interrupted';
const failures = new Map<string, Failure>();
const listeners = new Set<() => void>();
let version = 0;
let stopWatching: (() => void) | undefined;

function notify() {
  version++;
  listeners.forEach(l => l());
}

function retryInterrupted() {
  let changed = false;
  for (const [url, kind] of failures) {
    if (kind === 'interrupted') { failures.delete(url); changed = true; }
  }
  if (!changed) return;
  stopWatching?.();
  notify();
}

function subscribe(listener: () => void) {
  listeners.add(listener);
  return () => { listeners.delete(listener); };
}

function markFailed(url: string, kind: Failure) {
  failures.set(url, kind);
  if (kind === 'interrupted' && !stopWatching) {
    const offOnline = onlineManager.subscribe(online => online && retryInterrupted());
    const offFocus = focusManager.subscribe(focused => focused && retryInterrupted());
    stopWatching = () => { offOnline(); offFocus(); stopWatching = undefined; };
  }
  if (![...failures.values()].includes('interrupted')) stopWatching?.();
  notify();
}

// A load error can't say why it failed; one HEAD request can. An image response means the file is
// there and the load was interrupted; anything else (404, the HTML fallback page) means it isn't.
async function classify(url: string): Promise<Failure> {
  try {
    const res = await fetch(url, { method: 'HEAD' });
    return res.ok && (res.headers.get('content-type') ?? '').startsWith('image/') ? 'interrupted' : 'missing';
  } catch {
    return 'interrupted';
  }
}

export function resetTeamArtFailures() {
  failures.clear();
  stopWatching?.();
  notify();
}

// Shared "image with fallback-to-text-badge on load error" wiring for both TeamHelmet (synthetic
// SVG shield) and TeamLogo (real ESPN crest) — the only real difference between the two is which
// src/size/height they compute, not how a failed load is handled.
export default function TeamArtImage({ abbr, src, fallbackSrc, size, height, showLabel, cacheKey }: TeamArtImageProps) {
  useSyncExternalStore(subscribe, () => version);
  // Retries used per src (a theme switch's new src starts fresh), and the src waiting for its retry.
  const [retries, setRetries] = useState<Readonly<Record<string, number>>>({});
  const [waiting, setWaiting] = useState<string>();
  const retryTimer = useRef<ReturnType<typeof setTimeout>>(undefined);
  useEffect(() => () => clearTimeout(retryTimer.current), []);

  const current = [src, fallbackSrc].find((s): s is string => !!s && !failures.has(s));

  const onError = (failed: string) => {
    clearTimeout(retryTimer.current);
    const used = retries[failed] ?? 0;
    if (used < RETRY_DELAYS_MS.length) {
      setWaiting(failed);
      retryTimer.current = setTimeout(() => {
        setWaiting(undefined);
        setRetries(r => ({ ...r, [failed]: used + 1 }));
      }, RETRY_DELAYS_MS[used]);
      return;
    }
    setWaiting(failed);
    void classify(failed).then(kind => {
      setRetries(r => ({ ...r, [failed]: 0 }));
      setWaiting(undefined);
      markFailed(failed, kind);
    });
  };

  return (
    <TeamArtFrame abbr={abbr} size={size} showLabel={showLabel}>
      {!current || waiting === current ? (
        <TeamFallbackBadge abbr={abbr} size={size} height={height} />
      ) : (
        <img
          key={`${cacheKey ?? abbr}-${current}-${retries[current] ?? 0}`}
          src={current}
          width={size}
          height={height ?? size}
          alt={abbr}
          role="img"
          aria-label={abbr}
          style={{ display: 'block', objectFit: 'contain' }}
          onError={() => onError(current)}
        />
      )}
    </TeamArtFrame>
  );
}
