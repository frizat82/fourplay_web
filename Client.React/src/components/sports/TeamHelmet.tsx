import { getTeamColors } from './teamColors';

interface TeamHelmetProps {
  abbr: string;
  size?: number;
  flipped?: boolean;
  showLabel?: boolean; // show abbreviation below helmet (default true)
}

export default function TeamHelmet({ abbr, size = 56, flipped = false, showLabel = true }: TeamHelmetProps) {
  const { primary, secondary } = getTeamColors(abbr);
  const facemask = '#94a3b8';
  const id = `h${abbr.replace(/[^a-z0-9]/gi, '').toLowerCase()}`;

  return (
    <div
      style={{
        display: 'inline-flex',
        flexDirection: 'column',
        alignItems: 'center',
        gap: 2,
        flexShrink: 0,
      }}
    >
      <svg
        viewBox="0 0 80 68"
        width={size}
        height={size * 0.85}
        xmlns="http://www.w3.org/2000/svg"
        role="img"
        aria-label={abbr}
        style={{ transform: flipped ? 'scaleX(-1)' : undefined, display: 'block' }}
      >
        <defs>
          <filter id={`${id}s`} x="-8%" y="-8%" width="120%" height="130%">
            <feDropShadow dx="0" dy="2" stdDeviation="1.5" floodOpacity="0.3" />
          </filter>
        </defs>

        {/* ── Helmet dome — simple bold D-shape ── */}
        <path
          d="M 6,56 Q 2,28 14,13 Q 28,0 50,4 Q 68,8 70,30 Q 72,50 56,60 L 14,62 Z"
          fill={primary}
          filter={`url(#${id}s)`}
        />

        {/* ── Top stripe ── */}
        <path
          d="M 13,18 Q 38,7 62,14"
          fill="none"
          stroke={secondary}
          strokeWidth="6"
          strokeLinecap="round"
          opacity="0.9"
        />

        {/* ── Shine ── */}
        <ellipse cx="30" cy="16" rx="14" ry="7" fill="rgba(255,255,255,0.15)" transform="rotate(-20 30 16)" />

        {/* ── Earhole ── */}
        <circle cx="14" cy="46" r="5.5" fill="rgba(0,0,0,0.25)" />
        <circle cx="14" cy="46" r="3" fill={secondary} opacity="0.7" />

        {/* ── Chin strap ── */}
        <path
          d="M 14,62 Q 34,68 56,60"
          fill="none"
          stroke={secondary}
          strokeWidth="4"
          strokeLinecap="round"
          opacity="0.8"
        />

        {/* ── Facemask — 3 clean horizontal bars ── */}
        <path d="M 58,26 Q 74,24 76,32" fill="none" stroke={facemask} strokeWidth="3.5" strokeLinecap="round" />
        <path d="M 58,38 L 76,38"       fill="none" stroke={facemask} strokeWidth="3.5" strokeLinecap="round" />
        <path d="M 58,50 Q 74,52 76,44" fill="none" stroke={facemask} strokeWidth="3.5" strokeLinecap="round" />
        {/* Vertical connector */}
        <path d="M 76,32 L 76,44"       fill="none" stroke={facemask} strokeWidth="3.5" strokeLinecap="round" />
      </svg>

      {showLabel && (
        <span
          style={{
            fontSize: size < 40 ? 9 : size < 52 ? 10 : 11,
            fontWeight: 700,
            fontFamily: "'Arial Black', Arial, sans-serif",
            letterSpacing: '0.02em',
            lineHeight: 1,
            userSelect: 'none',
          }}
        >
          {abbr.toUpperCase()}
        </span>
      )}
    </div>
  );
}
