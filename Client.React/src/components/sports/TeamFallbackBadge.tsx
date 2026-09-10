interface TeamFallbackBadgeProps {
  abbr: string;
  size: number;
  height?: number;
}

// Plain text-in-a-box badge shown when a team's image asset (helmet SVG or real logo PNG) fails
// to load — e.g. an abbreviation with no downloaded logo yet. Shared by TeamHelmet and TeamLogo
// so both art modes fall back to the exact same look.
export default function TeamFallbackBadge({ abbr, size, height }: TeamFallbackBadgeProps) {
  return (
    <div
      role="img"
      aria-label={abbr}
      style={{
        width: size,
        height: height ?? size,
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
  );
}
