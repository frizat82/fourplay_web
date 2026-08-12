import { useState } from 'react';

interface TeamHelmetProps {
  abbr: string;
  size?: number;
  showLabel?: boolean;
}

export default function TeamHelmet({ abbr, size = 56, showLabel = true }: TeamHelmetProps) {
  const svgSrc = `/Icons/Helmets/${abbr.toLowerCase()}.svg`;
  const h = Math.round(size * 1.1);
  // frizat: a handful of teams (e.g. Illinois) only ever had the neon PNG set, never a flat SVG —
  // falling back to that PNG silently brought the neon look back for exactly those teams even
  // after the dark-mode neon revert. Fall back to a plain text badge instead so no team can ever
  // render the neon asset again, regardless of SVG coverage.
  const [svgFailed, setSvgFailed] = useState(false);

  return (
    <div style={{ display: 'inline-flex', flexDirection: 'column', alignItems: 'center', gap: 2, flexShrink: 0 }}>
      {svgFailed ? (
        <div
          role="img"
          aria-label={abbr}
          style={{
            width: size,
            height: h,
            borderRadius: '20%',
            display: 'flex',
            alignItems: 'center',
            justifyContent: 'center',
            backgroundColor: 'rgba(128, 128, 128, 0.25)',
            border: '1px solid rgba(128, 128, 128, 0.4)',
          }}
        >
          <span style={{
            fontSize: size < 44 ? 11 : size < 56 ? 13 : 15,
            fontWeight: 800,
            fontFamily: "'Arial Black', Arial, sans-serif",
            letterSpacing: '0.02em',
          }}>
            {abbr.toUpperCase()}
          </span>
        </div>
      ) : (
        <img
          key={abbr}
          src={svgSrc}
          width={size}
          height={h}
          alt={abbr}
          role="img"
          aria-label={abbr}
          style={{ display: 'block', objectFit: 'contain' }}
          onError={() => setSvgFailed(true)}
        />
      )}
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
