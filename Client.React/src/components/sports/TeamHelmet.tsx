import { useThemeMode } from '../../services/theme';

interface TeamHelmetProps {
  abbr: string;
  size?: number;
  showLabel?: boolean;
}

export default function TeamHelmet({ abbr, size = 56, showLabel = true }: TeamHelmetProps) {
  const { mode } = useThemeMode();
  const pngSrc = `/Icons/Helmets/${abbr.toLowerCase()}.png`;
  const svgSrc = `/Icons/Helmets/${abbr.toLowerCase()}.svg`;
  // The neon PNG set is designed to glow against a dark background and washes out on light
  // ones; the flat-color legacy SVG shields read better in light mode. Try the theme-matched
  // asset first and fall back to the other set for the handful of teams missing from either.
  const primarySrc = mode === 'dark' ? pngSrc : svgSrc;
  const fallbackSrc = mode === 'dark' ? svgSrc : pngSrc;
  const h = Math.round(size * 1.1);

  return (
    <div style={{ display: 'inline-flex', flexDirection: 'column', alignItems: 'center', gap: 2, flexShrink: 0 }}>
      <img
        key={`${abbr}-${mode}`}
        src={primarySrc}
        width={size}
        height={h}
        alt={abbr}
        role="img"
        aria-label={abbr}
        style={{ display: 'block', objectFit: 'contain' }}
        onError={(e) => {
          const img = e.target as HTMLImageElement;
          if (!img.dataset.fallbackTried) {
            img.dataset.fallbackTried = 'true';
            img.src = fallbackSrc;
            return;
          }
          img.style.visibility = 'hidden';
        }}
      />
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
