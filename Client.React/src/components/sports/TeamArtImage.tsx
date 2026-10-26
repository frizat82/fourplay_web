import { useEffect, useRef, useState } from 'react';
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
const RETRY_DELAYS_MS = [1000, 4000];

// Shared "image with fallback-to-text-badge on load error" wiring for both TeamHelmet (synthetic
// SVG shield) and TeamLogo (real ESPN crest) — the only real difference between the two is which
// src/size/height they compute, not how a failed load is handled.
export default function TeamArtImage({ abbr, src, fallbackSrc, size, height, showLabel, cacheKey }: TeamArtImageProps) {
  // Track failures by URL, not a single flag, so a theme switch (new src) gets a fresh attempt.
  const [failedSrcs, setFailedSrcs] = useState<ReadonlySet<string>>(new Set());
  const [attempt, setAttempt] = useState(0);
  const retryTimer = useRef<ReturnType<typeof setTimeout>>(undefined);
  const current = [src, fallbackSrc].find((s): s is string => !!s && !failedSrcs.has(s));

  // Anything that gave up gets a fresh try once the phone is back online or the app is in front again.
  useEffect(() => {
    const reset = () => {
      if (document.visibilityState === 'hidden') return;
      setFailedSrcs(prev => (prev.size ? new Set() : prev));
      setAttempt(0);
    };
    window.addEventListener('online', reset);
    document.addEventListener('visibilitychange', reset);
    return () => {
      window.removeEventListener('online', reset);
      document.removeEventListener('visibilitychange', reset);
      clearTimeout(retryTimer.current);
    };
  }, []);

  const onError = (failed: string) => {
    if (attempt < RETRY_DELAYS_MS.length) {
      retryTimer.current = setTimeout(() => setAttempt(a => a + 1), RETRY_DELAYS_MS[attempt]);
      return;
    }
    setAttempt(0);
    setFailedSrcs(prev => new Set(prev).add(failed));
  };

  return (
    <TeamArtFrame abbr={abbr} size={size} showLabel={showLabel}>
      {!current ? (
        <TeamFallbackBadge abbr={abbr} size={size} height={height} />
      ) : (
        <img
          key={`${cacheKey ?? abbr}-${current}-${attempt}`}
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
