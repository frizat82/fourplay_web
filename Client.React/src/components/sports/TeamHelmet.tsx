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
  // frizat: reverted the dark-mode neon PNG set — it wasn't reading well in practice. Flat-color
  // SVG shield badges are used in both modes now; PNG stays only as a fallback for the handful of
  // teams missing an SVG.
  const primarySrc = svgSrc;
  const fallbackSrc = pngSrc;
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
