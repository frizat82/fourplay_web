import { useState } from 'react';
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

// Shared "image with fallback-to-text-badge on load error" wiring for both TeamHelmet (synthetic
// SVG shield) and TeamLogo (real ESPN crest) — the only real difference between the two is which
// src/size/height they compute, not how a failed load is handled.
export default function TeamArtImage({ abbr, src, fallbackSrc, size, height, showLabel, cacheKey }: TeamArtImageProps) {
  // Track failures by URL, not a single flag, so a theme switch (new src) gets a fresh attempt.
  const [failedSrcs, setFailedSrcs] = useState<ReadonlySet<string>>(new Set());
  const current = [src, fallbackSrc].find((s): s is string => !!s && !failedSrcs.has(s));

  return (
    <TeamArtFrame abbr={abbr} size={size} showLabel={showLabel}>
      {!current ? (
        <TeamFallbackBadge abbr={abbr} size={size} height={height} />
      ) : (
        <img
          key={`${cacheKey ?? abbr}-${current}`}
          src={current}
          width={size}
          height={height ?? size}
          alt={abbr}
          role="img"
          aria-label={abbr}
          style={{ display: 'block', objectFit: 'contain' }}
          onError={() => setFailedSrcs((prev) => new Set(prev).add(current))}
        />
      )}
    </TeamArtFrame>
  );
}
