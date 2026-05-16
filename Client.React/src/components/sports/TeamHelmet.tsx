import { getTeamColors } from './teamColors';

interface TeamHelmetProps {
  abbr: string;
  size?: number;
  flipped?: boolean;
  showLabel?: boolean;
}

export default function TeamHelmet({ abbr, size = 56, flipped = false, showLabel = true }: TeamHelmetProps) {
  const { primary, secondary } = getTeamColors(abbr);
  const cage = '#8896aa';
  const id = `h${abbr.replace(/[^a-z0-9]/gi, '').toLowerCase()}${size}`;

  return (
    <div style={{ display: 'inline-flex', flexDirection: 'column', alignItems: 'center', gap: 2, flexShrink: 0 }}>
      <svg
        viewBox="0 0 110 90"
        width={size}
        height={size * (90 / 110)}
        xmlns="http://www.w3.org/2000/svg"
        role="img"
        aria-label={abbr}
        style={{ transform: flipped ? 'scaleX(-1)' : undefined, display: 'block' }}
      >
        <defs>
          {/* Clip: dome stays inside left 70% so face opens on the right */}
          <clipPath id={`${id}c`}>
            <rect x="0" y="0" width="68" height="90" />
          </clipPath>
          <filter id={`${id}s`} x="-10%" y="-10%" width="125%" height="130%">
            <feDropShadow dx="0" dy="2" stdDeviation="2" floodOpacity="0.25" />
          </filter>
        </defs>

        {/* ── Dome — large circle clipped to leave face opening ── */}
        <circle
          cx="38" cy="38" r="36"
          fill={primary}
          filter={`url(#${id}s)`}
        />

        {/* ── Chin/jaw extension ── */}
        <path
          d="M 5,58 Q 4,76 22,80 Q 38,84 55,74 L 55,68 Q 40,78 24,74 Q 10,70 10,58 Z"
          fill={primary}
        />

        {/* ── Stripe across the top ── */}
        <path
          d="M 12,10 Q 36,2 62,10"
          fill="none"
          stroke={secondary}
          strokeWidth="7"
          strokeLinecap="round"
          clipPath={`url(#${id}c)`}
          opacity="0.9"
        />

        {/* ── Shine highlight ── */}
        <ellipse
          cx="26" cy="18" rx="12" ry="6"
          fill="rgba(255,255,255,0.18)"
          transform="rotate(-25 26 18)"
          clipPath={`url(#${id}c)`}
        />

        {/* ── Earhole ── */}
        <circle cx="14" cy="50" r="5" fill="rgba(0,0,0,0.22)" />
        <circle cx="14" cy="50" r="3" fill={secondary} opacity="0.65" />

        {/* ── Face opening edge / shadow ── */}
        <path
          d="M 62,6 Q 72,16 72,38 Q 72,58 60,70"
          fill="none"
          stroke="rgba(0,0,0,0.18)"
          strokeWidth="8"
          strokeLinecap="round"
        />

        {/* ── Facemask cage ──
             Top clip → top bar → 2 horizontal bars → bottom bar → bottom clip
             Vertical bar on the right connects them ── */}

        {/* Top clip (rectangular connector) */}
        <rect x="58" y="14" width="10" height="7" rx="2" fill={cage} />

        {/* Bottom clip */}
        <rect x="58" y="62" width="10" height="7" rx="2" fill={cage} />

        {/* Top diagonal bar to cage */}
        <path d="M 67,17 L 82,22" stroke={cage} strokeWidth="4" strokeLinecap="round" />

        {/* Horizontal bar 1 */}
        <path d="M 68,32 L 90,32" stroke={cage} strokeWidth="3.5" strokeLinecap="round" />

        {/* Horizontal bar 2 */}
        <path d="M 68,46 L 91,46" stroke={cage} strokeWidth="3.5" strokeLinecap="round" />

        {/* Bottom diagonal bar from cage */}
        <path d="M 67,66 L 82,60" stroke={cage} strokeWidth="4" strokeLinecap="round" />

        {/* Right vertical connector */}
        <path d="M 90,22 L 91,60" stroke={cage} strokeWidth="4" strokeLinecap="round" />

        {/* Left vertical bar along face edge */}
        <path d="M 68,17 L 68,66" stroke={cage} strokeWidth="3" strokeLinecap="round" />
      </svg>

      {showLabel && (
        <span style={{
          fontSize: size < 40 ? 9 : size < 52 ? 10 : 11,
          fontWeight: 700,
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
