import { useEffect, useId, useState } from 'react';
import { Box, Button, Dialog, Stack, Typography, useMediaQuery } from '@mui/material';
import { alpha, keyframes } from '@mui/material/styles';
import EmojiEventsIcon from '@mui/icons-material/EmojiEvents';
import TeamArt from './sports/TeamArt';
import { formatMargin } from '../utils/survivorMatrix';
import type { CelebrationPick } from '../utils/weekWin';

interface WeekWinCelebrationProps {
  sport: 'nfl' | 'cfb';
  leagueName: string;
  weekLabel: string;
  /** What the viewer collects. 0 when everyone won and the pot rolls over. */
  amount: number;
  /** The viewer's covered picks. Empty when they couldn't be loaded. */
  picks: CelebrationPick[];
  onDone: () => void;
}

// The sequence, in ms: trophy, heading, one stamp per pick, then the winnings count up.
const HEADING_AT = 450;
const FIRST_PICK_AT = 900;
const PICK_EVERY = 250;
const COUNT_UP_FOR = 900;

const CONFETTI_COUNT = 36;
const CONFETTI_COLORS = ['success.main', 'secondary.main', 'info.main', 'warning.main'] as const;

const pop = keyframes`
  0% { transform: scale(0); opacity: 0; }
  70% { transform: scale(1.15); opacity: 1; }
  100% { transform: scale(1); opacity: 1; }
`;
const rise = keyframes`
  from { transform: translateY(12px); opacity: 0; }
  to { transform: translateY(0); opacity: 1; }
`;
const stamp = keyframes`
  from { transform: scale(1.6); opacity: 0; }
  to { transform: scale(1); opacity: 1; }
`;
const fall = keyframes`
  from { transform: translateY(0) rotate(0deg); }
  to { transform: translateY(125vh) rotate(720deg); }
`;

const REDUCED_MOTION = '(prefers-reduced-motion: reduce)';

// Only animates when the viewer hasn't asked for reduced motion; otherwise the element just shows.
const animated = (name: ReturnType<typeof keyframes>, durationMs: number, delayMs: number, easing = 'ease-out') => ({
  '@media (prefers-reduced-motion: no-preference)': {
    animation: `${name} ${durationMs}ms ${easing} ${delayMs}ms both`,
  },
});

/** Counts from 0 up to `target` once `delayMs` has passed. Returns `target` straight away when not animating. */
function useCountUp(target: number, delayMs: number, animate: boolean): number {
  const [value, setValue] = useState(0);
  useEffect(() => {
    if (!animate) return;
    let frame = 0;
    let startedAt: number | null = null;
    const tick = (now: number) => {
      startedAt ??= now;
      const progress = Math.min(1, (now - startedAt) / COUNT_UP_FOR);
      setValue(Math.round(target * (1 - (1 - progress) ** 3)));
      if (progress < 1) frame = requestAnimationFrame(tick);
    };
    const timer = setTimeout(() => { frame = requestAnimationFrame(tick); }, delayMs);
    return () => {
      clearTimeout(timer);
      cancelAnimationFrame(frame);
    };
  }, [target, delayMs, animate]);
  return animate ? value : target;
}

// Spread evenly from the index alone, so every render draws the same confetti.
function Confetti() {
  return (
    <Box data-testid="confetti" aria-hidden sx={{ position: 'absolute', inset: 0, overflow: 'hidden', pointerEvents: 'none' }}>
      {Array.from({ length: CONFETTI_COUNT }, (_, i) => (
        <Box
          key={i}
          sx={{
            position: 'absolute',
            top: '-24px',
            left: `${(i * 37) % 100}%`,
            width: 8,
            height: i % 3 === 0 ? 8 : 14,
            borderRadius: i % 3 === 0 ? '50%' : '2px',
            bgcolor: CONFETTI_COLORS[i % CONFETTI_COLORS.length],
            animation: `${fall} ${2400 + (i % 5) * 350}ms linear ${(i % 12) * 130}ms both`,
          }}
        />
      ))}
    </Box>
  );
}

function pickLabel({ team, pickType, margin }: CelebrationPick): string {
  const name = pickType === 'Spread' ? team : `${team} ${pickType}`;
  return margin == null ? `${name} covered` : `${name} covered by ${margin}`;
}

/**
 * Full-screen celebration for winning a week: trophy, the covered picks, and the winnings counting
 * up. Silent. A tap anywhere, the Continue button or Escape closes it.
 */
export default function WeekWinCelebration({ sport, leagueName, weekLabel, amount, picks, onDone }: WeekWinCelebrationProps) {
  const titleId = useId();
  const reducedMotion = useMediaQuery(REDUCED_MOTION, { noSsr: true });
  const amountAt = FIRST_PICK_AT + picks.length * PICK_EVERY + 150;
  const shownAmount = useCountUp(amount, amountAt, !reducedMotion);

  return (
    <Dialog open fullScreen onClose={onDone} aria-labelledby={titleId}>
      <Box
        onClick={onDone}
        sx={theme => ({
          position: 'relative',
          minHeight: '100%',
          display: 'flex',
          flexDirection: 'column',
          alignItems: 'center',
          justifyContent: 'center',
          gap: 2,
          px: 3,
          py: 4,
          textAlign: 'center',
          cursor: 'pointer',
          overflow: 'hidden',
          bgcolor: 'background.default',
          // Confetti falls behind the content, so it never covers a number.
          '& > :not([data-testid="confetti"])': { position: 'relative', zIndex: 1 },
          backgroundImage: `radial-gradient(circle at 50% 28%, ${alpha(theme.palette.success.main, 0.22)}, transparent 62%)`,
        })}
      >
        {!reducedMotion && <Confetti />}

        <EmojiEventsIcon sx={{ fontSize: 112, color: 'secondary.main', ...animated(pop, 600, 100) }} />

        <Box sx={animated(rise, 400, HEADING_AT)}>
          <Typography variant="overline" color="text.secondary" sx={{ letterSpacing: '0.12em' }}>
            {leagueName} · {weekLabel}
          </Typography>
          <Typography id={titleId} variant="h3" component="h2" fontWeight={800} sx={{ fontSize: { xs: '2.25rem', sm: '3rem' } }}>
            You won the week!
          </Typography>
          {picks.length > 0 && (
            <Typography variant="subtitle1" color="text.secondary">
              {picks.length} for {picks.length}
            </Typography>
          )}
        </Box>

        {picks.length > 0 && (
          <Stack direction="row" spacing={2} justifyContent="center" flexWrap="wrap" useFlexGap>
            {picks.map((pick, i) => (
              <Box
                key={`${pick.team}-${pick.pickType}`}
                role="img"
                aria-label={pickLabel(pick)}
                sx={{ display: 'flex', flexDirection: 'column', alignItems: 'center', ...animated(stamp, 350, FIRST_PICK_AT + i * PICK_EVERY) }}
              >
                <TeamArt abbr={pick.team} sport={sport} size={48} showLabel />
                <Typography variant="body2" fontWeight={700} color="success.main">
                  {pick.pickType !== 'Spread' && `${pick.pickType} `}
                  {pick.margin != null && formatMargin(pick.margin)}
                </Typography>
              </Box>
            ))}
          </Stack>
        )}

        <Box sx={animated(rise, 400, amountAt)}>
          {amount > 0 ? (
            <Typography variant="h2" component="p" fontWeight={800} color="success.main" aria-label={`You collect $${amount}`}>
              +${shownAmount}
            </Typography>
          ) : (
            <Typography variant="body1" color="text.secondary">
              Everyone covered, so the pot rolls over to next week.
            </Typography>
          )}
        </Box>

        {/* Its click bubbles to the tap-anywhere handler above, so it needs none of its own. */}
        <Button variant="contained" color="secondary" size="large" sx={{ mt: 2, minHeight: 44, minWidth: 160 }}>
          Continue
        </Button>
        <Typography variant="caption" color="text.secondary">
          Tap anywhere to continue
        </Typography>
      </Box>
    </Dialog>
  );
}
