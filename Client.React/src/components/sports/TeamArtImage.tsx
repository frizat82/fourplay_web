import { useState } from 'react';
import TeamArtFrame from './TeamArtFrame';
import TeamFallbackBadge from './TeamFallbackBadge';

interface TeamArtImageProps {
  abbr: string;
  src: string;
  size: number;
  height?: number;
  showLabel: boolean;
  cacheKey?: string;
}

// Shared "image with fallback-to-text-badge on load error" wiring for both TeamHelmet (synthetic
// SVG shield) and TeamLogo (real ESPN crest) — the only real difference between the two is which
// src/size/height they compute, not how a failed load is handled.
export default function TeamArtImage({ abbr, src, size, height, showLabel, cacheKey }: TeamArtImageProps) {
  const [failed, setFailed] = useState(false);

  return (
    <TeamArtFrame abbr={abbr} size={size} showLabel={showLabel}>
      {failed ? (
        <TeamFallbackBadge abbr={abbr} size={size} height={height} />
      ) : (
        <img
          key={cacheKey ?? abbr}
          src={src}
          width={size}
          height={height ?? size}
          alt={abbr}
          role="img"
          aria-label={abbr}
          style={{ display: 'block', objectFit: 'contain' }}
          onError={() => setFailed(true)}
        />
      )}
    </TeamArtFrame>
  );
}
