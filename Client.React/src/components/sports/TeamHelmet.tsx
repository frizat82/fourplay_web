import { getTeamColors } from './teamColors';

interface TeamHelmetProps {
  abbr: string;
  size?: number;
  flipped?: boolean;
  showLabel?: boolean;
}

export default function TeamHelmet({ abbr, size = 56, flipped = false, showLabel = true }: TeamHelmetProps) {
  const { primary, secondary } = getTeamColors(abbr);
  const cage = '#94a3b8';
  const id = `h${abbr.replace(/[^a-z0-9]/gi, '').toLowerCase()}`;

  return (
    <div style={{ display: 'inline-flex', flexDirection: 'column', alignItems: 'center', gap: 2, flexShrink: 0 }}>
      <svg
        viewBox="0 0 100 82"
        width={size}
        height={Math.round(size * 82 / 100)}
        xmlns="http://www.w3.org/2000/svg"
        role="img"
        aria-label={abbr}
        style={{ transform: flipped ? 'scaleX(-1)' : undefined, display: 'block', overflow: 'visible' }}
      >
        <defs>
          <filter id={`${id}d`} x="-15%" y="-15%" width="135%" height="140%">
            <feDropShadow dx="0" dy="2" stdDeviation="2" floodOpacity="0.28" />
          </filter>
        </defs>

        {/* ── Helmet dome with face opening ──
            Dome is a large rounded shape facing right.
            Face opening is a natural concave on the right side. ── */}
        <path
          d="M 8,68
             C 2,52 2,28 10,12
             C 18,0 34,0 52,4
             C 68,8 76,18 76,34
             C 76,46 70,56 62,64
             C 56,70 42,74 22,74
             C 14,74 8,72 8,68 Z"
          fill={primary}
          filter={`url(#${id}d)`}
        />

        {/* ── Top stripe ── */}
        <path
          d="M 12,12 Q 40,4 66,14"
          fill="none"
          stroke={secondary}
          strokeWidth="6"
          strokeLinecap="round"
          opacity="0.85"
        />

        {/* ── Shine ── */}
        <ellipse
          cx="28" cy="18"
          rx="13" ry="5"
          fill="rgba(255,255,255,0.2)"
          transform="rotate(-22 28 18)"
        />

        {/* ── Earhole ── */}
        <circle cx="14" cy="52" r="5.5" fill="rgba(0,0,0,0.25)" />
        <circle cx="14" cy="52" r="3.5" fill={secondary} opacity="0.7" />

        {/* ── Chin strap ── */}
        <path
          d="M 16,72 Q 36,80 58,68"
          fill="none"
          stroke={secondary}
          strokeWidth="4"
          strokeLinecap="round"
          opacity="0.75"
        />

        {/* ── Face opening shadow (right edge of dome) ── */}
        <path
          d="M 62,8 C 76,14 80,24 80,36 C 80,50 74,62 62,70"
          fill="none"
          stroke="rgba(0,0,0,0.15)"
          strokeWidth="6"
        />

        {/* ── Facemask cage ──
            Attaches at two clips on the face opening, extends right as a rectangular cage ── */}

        {/* Top clip */}
        <rect x="60" y="12" width="10" height="7" rx="2.5" fill={cage} />
        {/* Bottom clip */}
        <rect x="60" y="60" width="10" height="7" rx="2.5" fill={cage} />

        {/* Left vertical bar (along face edge) */}
        <line x1="69" y1="15" x2="69" y2="64" stroke={cage} strokeWidth="3" strokeLinecap="round" />

        {/* Top angled bar to cage */}
        <line x1="69" y1="16" x2="86" y2="22" stroke={cage} strokeWidth="4" strokeLinecap="round" />

        {/* Horizontal bar 1 */}
        <line x1="69" y1="33" x2="90" y2="33" stroke={cage} strokeWidth="3.5" strokeLinecap="round" />

        {/* Horizontal bar 2 */}
        <line x1="69" y1="47" x2="90" y2="47" stroke={cage} strokeWidth="3.5" strokeLinecap="round" />

        {/* Bottom angled bar */}
        <line x1="69" y1="64" x2="86" y2="58" stroke={cage} strokeWidth="4" strokeLinecap="round" />

        {/* Right vertical connector */}
        <line x1="90" y1="22" x2="90" y2="58" stroke={cage} strokeWidth="4" strokeLinecap="round" />
      </svg>

      {showLabel && (
        <span style={{
          fontSize: size < 40 ? 9 : size < 52 ? 10 : 11,
          fontWeight: 800,
          fontFamily: "'Arial Black', Arial, sans-serif",
          letterSpacing: '0.02em',
          lineHeight: 1,
          userSelect: 'none',
        }}>
          {abbr.toUpperCase()}
        </span>
      )}
    </div>
  );
}
