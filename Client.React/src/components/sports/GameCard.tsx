import { Box, Button, Card, CardContent, Chip, Stack, Typography } from '@mui/material';
import TeamHelmet from './TeamHelmet';

export type PickState = 'none' | 'pending' | 'submitted';

export interface GameCardProps {
  homeTeam: string;
  awayTeam: string;
  homeSpread: number;
  awaySpread: number;
  overUnder: number;
  gameTime: string;
  mode: 'pick' | 'score';
  // Score mode
  homeScore?: number;
  awayScore?: number;
  gameStatus?: string; // 'StatusFinal' | 'StatusInProgress' | 'StatusScheduled'
  // Pick mode
  homePickState?: PickState;
  awayPickState?: PickState;
  locked?: boolean;
  onPickHome?: () => void;
  onPickAway?: () => void;
  // Optional: who in the league picked what (score mode)
  homePickers?: number;
  awayPickers?: number;
}

function spreadLabel(spread: number): string {
  if (spread === 0) return 'PK';
  return spread > 0 ? `+${spread}` : `${spread}`;
}

function statusChip(status: string | undefined, gameTime: string) {
  if (!status || status === 'StatusScheduled') {
    return (
      <Typography variant="caption" color="text.secondary">
        {new Date(gameTime).toLocaleString([], { weekday: 'short', month: 'short', day: 'numeric', hour: 'numeric', minute: '2-digit' })}
      </Typography>
    );
  }
  if (status === 'StatusFinal') return <Chip label="Final" size="small" color="default" />;
  if (status === 'StatusInProgress') return <Chip label="Live" size="small" color="success" />;
  return null;
}

function pickButtonColor(state: PickState): 'secondary' | 'success' | 'inherit' {
  if (state === 'submitted') return 'success';
  if (state === 'pending') return 'secondary';
  return 'inherit';
}

export default function GameCard({
  homeTeam, awayTeam, homeSpread, awaySpread, overUnder, gameTime,
  mode,
  homeScore, awayScore, gameStatus,
  homePickState = 'none', awayPickState = 'none',
  locked = false,
  onPickHome, onPickAway,
  homePickers, awayPickers,
}: GameCardProps) {
  const isFinal = gameStatus === 'StatusFinal';
  const isLive = gameStatus === 'StatusInProgress';

  return (
    <Card elevation={2} sx={{ height: '100%' }}>
      <CardContent sx={{ pb: '12px !important' }}>
        {/* Status row */}
        <Box sx={{ mb: 1 }}>
          {statusChip(gameStatus, gameTime)}
        </Box>

        {/* Score (score mode) */}
        {mode === 'score' && (isFinal || isLive) && (
          <Typography variant="h6" fontWeight={700} sx={{ mb: 1 }}>
            {homeTeam} {homeScore ?? 0} – {awayScore ?? 0} {awayTeam}
          </Typography>
        )}

        {/* Teams row */}
        <Stack direction="row" alignItems="center" spacing={1}>
          {/* Home team */}
          <Stack alignItems="center" spacing={0.5} sx={{ flex: 1 }}>
            <TeamHelmet abbr={homeTeam} size={56} />
            <Typography variant="caption" fontWeight={700}>{homeTeam}</Typography>
            {mode === 'pick' ? (
              <Button
                size="small"
                fullWidth
                variant={homePickState !== 'none' ? 'contained' : 'outlined'}
                color={pickButtonColor(homePickState)}
                disabled={locked && homePickState === 'none'}
                onClick={onPickHome}
                sx={{ fontSize: '0.7rem', py: 0.5 }}
              >
                {spreadLabel(homeSpread)}
              </Button>
            ) : (
              <Typography variant="caption" color="text.secondary">
                {spreadLabel(homeSpread)}
                {homePickers !== undefined && ` · ${homePickers}👤`}
              </Typography>
            )}
          </Stack>

          {/* Center: O/U */}
          <Stack alignItems="center" sx={{ minWidth: 48 }}>
            <Typography variant="caption" color="text.secondary" fontWeight={600}>O/U</Typography>
            <Typography variant="body2" fontWeight={700}>{overUnder}</Typography>
          </Stack>

          {/* Away team */}
          <Stack alignItems="center" spacing={0.5} sx={{ flex: 1 }}>
            <TeamHelmet abbr={awayTeam} size={48} flipped />
            <Typography variant="caption" fontWeight={700}>{awayTeam}</Typography>
            {mode === 'pick' ? (
              <Button
                size="small"
                fullWidth
                variant={awayPickState !== 'none' ? 'contained' : 'outlined'}
                color={pickButtonColor(awayPickState)}
                disabled={locked && awayPickState === 'none'}
                onClick={onPickAway}
                sx={{ fontSize: '0.7rem', py: 0.5 }}
              >
                {spreadLabel(awaySpread)}
              </Button>
            ) : (
              <Typography variant="caption" color="text.secondary">
                {spreadLabel(awaySpread)}
                {awayPickers !== undefined && ` · ${awayPickers}👤`}
              </Typography>
            )}
          </Stack>
        </Stack>
      </CardContent>
    </Card>
  );
}
