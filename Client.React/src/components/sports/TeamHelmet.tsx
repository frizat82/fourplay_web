import { getTeamColors } from './teamColors';

interface TeamHelmetProps {
  abbr: string;
  size?: number;
  flipped?: boolean; // flip horizontally so away team faces home team
}

export default function TeamHelmet({ abbr, size = 56, flipped = false }: TeamHelmetProps) {
  const { primary, secondary, text } = getTeamColors(abbr);
  const facemask = '#9ca3af';
  const fontSize = abbr.length >= 4 ? 13 : abbr.length === 3 ? 15 : 17;

  return (
    <svg
      viewBox="0 0 100 80"
      width={size}
      height={size * 0.8}
      xmlns="http://www.w3.org/2000/svg"
      style={{ transform: flipped ? 'scaleX(-1)' : undefined, display: 'block' }}
      role="img"
      aria-label={abbr}
    >
      {/* ── Drop shadow ── */}
      <defs>
        <filter id={`shadow-${abbr}`} x="-10%" y="-10%" width="120%" height="130%">
          <feDropShadow dx="0" dy="2" stdDeviation="2" floodOpacity="0.25" />
        </filter>
      </defs>

      {/* ── Helmet dome (main body) ── */}
      <path
        d="M 12,62 Q 4,34 18,16 Q 36,0 62,5 Q 84,10 86,36 Q 88,58 68,68 L 22,70 Z"
        fill={primary}
        filter={`url(#shadow-${abbr})`}
      />

      {/* ── Helmet shine highlight ── */}
      <path
        d="M 22,14 Q 42,4 64,10 Q 50,8 30,18 Z"
        fill="rgba(255,255,255,0.18)"
      />

      {/* ── Top stripe ── */}
      <path
        d="M 18,22 Q 44,10 72,18"
        fill="none"
        stroke={secondary}
        strokeWidth="7"
        strokeLinecap="round"
        opacity="0.9"
      />

      {/* ── Face opening (darker panel) ── */}
      <path
        d="M 68,68 Q 88,58 86,36 Q 84,18 70,10 L 74,10 Q 90,20 90,40 Q 90,62 72,72 Z"
        fill="rgba(0,0,0,0.22)"
      />

      {/* ── Earhole ── */}
      <circle cx="20" cy="52" r="6" fill="rgba(0,0,0,0.3)" />
      <circle cx="20" cy="52" r="3.5" fill={secondary} opacity="0.7" />

      {/* ── Chin strap ── */}
      <path
        d="M 22,70 Q 44,78 68,68"
        fill="none"
        stroke={secondary}
        strokeWidth="5"
        strokeLinecap="round"
        opacity="0.85"
      />

      {/* ── Facemask bars ── */}
      <path d="M 72,22 Q 92,24 94,34" fill="none" stroke={facemask} strokeWidth="4.5" strokeLinecap="round" />
      <path d="M 72,36 L 95,36"        fill="none" stroke={facemask} strokeWidth="4.5" strokeLinecap="round" />
      <path d="M 72,50 Q 92,50 94,42"  fill="none" stroke={facemask} strokeWidth="4.5" strokeLinecap="round" />
      {/* Vertical connector */}
      <path d="M 94,34 L 94,42"        fill="none" stroke={facemask} strokeWidth="4.5" strokeLinecap="round" />

      {/* ── Team abbreviation — stroke outline for readability ── */}
      <text
        x="37"
        y="46"
        fontFamily="'Arial Black', Impact, Arial, sans-serif"
        fontWeight="900"
        fontSize={fontSize}
        fill="rgba(0,0,0,0.4)"
        textAnchor="middle"
        dominantBaseline="middle"
        stroke="rgba(0,0,0,0.3)"
        strokeWidth="3"
        style={{ letterSpacing: '-0.5px' }}
      >
        {abbr.toUpperCase()}
      </text>
      <text
        x="37"
        y="46"
        fontFamily="'Arial Black', Impact, Arial, sans-serif"
        fontWeight="900"
        fontSize={fontSize}
        fill={text}
        textAnchor="middle"
        dominantBaseline="middle"
        style={{ letterSpacing: '-0.5px' }}
      >
        {abbr.toUpperCase()}
      </text>
    </svg>
  );
}
