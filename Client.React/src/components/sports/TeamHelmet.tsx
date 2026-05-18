interface TeamHelmetProps {
  abbr: string;
  size?: number;
  flipped?: boolean;
  showLabel?: boolean;
}

export default function TeamHelmet({ abbr, size = 56, flipped = false, showLabel = true }: TeamHelmetProps) {
  const src = `/Icons/Helmets/${abbr.toLowerCase()}.svg`;
  const h = Math.round(size * 1.1);

  return (
    <div style={{ display: 'inline-flex', flexDirection: 'column', alignItems: 'center', gap: 2, flexShrink: 0 }}>
      <img
        src={src}
        width={size}
        height={h}
        alt={abbr}
        role="img"
        aria-label={abbr}
        style={{ transform: flipped ? 'scaleX(-1)' : undefined, display: 'block', objectFit: 'contain' }}
        onError={(e) => { (e.target as HTMLImageElement).style.visibility = 'hidden'; }}
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
