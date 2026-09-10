import type { ReactNode } from 'react';

interface TeamArtFrameProps {
  abbr: string;
  size: number;
  showLabel: boolean;
  children: ReactNode;
}

// Shared layout (image/fallback + optional abbreviation caption) for both TeamHelmet (badges
// mode) and TeamLogo (logos mode) — keeps their visual frame identical regardless of which art
// source is active.
export default function TeamArtFrame({ abbr, size, showLabel, children }: TeamArtFrameProps) {
  return (
    <div style={{ display: 'inline-flex', flexDirection: 'column', alignItems: 'center', gap: 2, flexShrink: 0 }}>
      {children}
      {showLabel && (
        <span style={{
          fontSize: size < 44 ? 9 : size < 56 ? 10 : 11,
          fontWeight: 800,
          fontFamily: "'Arial Black', Arial, sans-serif",
          letterSpacing: '0.03em',
          lineHeight: 1,
          userSelect: 'none',
        }}>
          {abbr.toUpperCase()}
        </span>
      )}
    </div>
  );
}
