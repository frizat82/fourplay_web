import { getTeamColors } from './teamColors';

interface TeamHelmetProps {
  abbr: string;
  size?: number;
  flipped?: boolean;
}

export default function TeamHelmet({ abbr, size = 56, flipped = false }: TeamHelmetProps) {
  const { primary, secondary, text } = getTeamColors(abbr);
  const facemask = '#94a3b8';
  const id = `hm-${abbr.replace(/[^a-zA-Z0-9]/g, '')}`;
  const fontSize = abbr.length >= 4 ? 14 : abbr.length === 3 ? 16 : 19;

  return (
    <svg
      viewBox="0 0 100 80"
      width={size}
      height={size * 0.8}
      xmlns="http://www.w3.org/2000/svg"
      role="img"
      aria-label={abbr}
      style={{ transform: flipped ? 'scaleX(-1)' : undefined, display: 'block', flexShrink: 0 }}
    >
      <defs>
        <filter id={`${id}-sh`} x="-5%" y="-5%" width="115%" height="125%">
          <feDropShadow dx="0" dy="2" stdDeviation="2" floodOpacity="0.3" />
        </filter>
      </defs>

      {/* ── Helmet dome ── */}
      <path
        d="M 10,64 Q 2,36 16,18 Q 34,1 62,6 Q 84,11 86,38 Q 88,60 66,70 L 20,72 Z"
        fill={primary}
        filter={`url(#${id}-sh)`}
      />

      {/* ── Shine ── */}
      <path
        d="M 20,16 Q 44,5 68,12 Q 50,7 28,20 Z"
        fill="rgba(255,255,255,0.2)"
      />

      {/* ── Top stripe ── */}
      <path
        d="M 16,24 Q 44,12 72,20"
        fill="none"
        stroke={secondary}
        strokeWidth="7"
        strokeLinecap="round"
        opacity="0.9"
      />

      {/* ── Face opening shadow ── */}
      <path
        d="M 66,70 Q 88,60 86,38 Q 84,18 70,10 L 74,12 Q 90,22 90,42 Q 90,64 70,74 Z"
        fill="rgba(0,0,0,0.2)"
      />

      {/* ── Earhole ── */}
      <circle cx="18" cy="54" r="6.5" fill="rgba(0,0,0,0.28)" />
      <circle cx="18" cy="54" r="4" fill={secondary} opacity="0.75" />

      {/* ── Chin strap ── */}
      <path
        d="M 20,72 Q 44,80 66,70"
        fill="none"
        stroke={secondary}
        strokeWidth="5"
        strokeLinecap="round"
        opacity="0.85"
      />

      {/* ── Facemask — lower position ── */}
      <path d="M 72,38 Q 90,34 93,42" fill="none" stroke={facemask} strokeWidth="4" strokeLinecap="round" />
      <path d="M 72,50 L 94,50"       fill="none" stroke={facemask} strokeWidth="4" strokeLinecap="round" />
      <path d="M 72,62 Q 90,64 93,56" fill="none" stroke={facemask} strokeWidth="4" strokeLinecap="round" />
      <path d="M 93,42 L 93,56"       fill="none" stroke={facemask} strokeWidth="4" strokeLinecap="round" />

      {/* ── Abbreviation — shadow layer ── */}
      <text
        x="36" y="48"
        fontFamily="'Arial Black', Impact, Arial, sans-serif"
        fontWeight="900"
        fontSize={fontSize}
        fill="rgba(0,0,0,0.45)"
        stroke="rgba(0,0,0,0.3)"
        strokeWidth="3"
        textAnchor="middle"
        dominantBaseline="middle"
      >
        {abbr.toUpperCase()}
      </text>
      {/* ── Abbreviation — foreground ── */}
      <text
        x="36" y="48"
        fontFamily="'Arial Black', Impact, Arial, sans-serif"
        fontWeight="900"
        fontSize={fontSize}
        fill={text}
        textAnchor="middle"
        dominantBaseline="middle"
      >
        {abbr.toUpperCase()}
      </text>
    </svg>
  );
}
