import { Box, Button, Card, CardContent, Chip, Stack, Typography } from '@mui/material';
import CheckIcon from '@mui/icons-material/Check';
import TeamHelmet from './TeamHelmet';
import WeatherIcon from '../WeatherIcon';
import { spreadLabel } from '../../utils/gameHelpers';
import { toLocalDisplay } from '../../utils/time';

export type PickState = 'none' | 'pending' | 'submitted';

export interface GameCardProps {
  homeTeam: string;
  awayTeam: string;
  homeSpread: number | null;
  awaySpread: number | null;
  overUnder?: number;
  gameTime: string;
  mode: 'pick' | 'score';
  // Score display
  homeScore?: number;
  awayScore?: number;
  gameStatus?: string;
  gameDetail?: string;
  // Pick mode
  homePickState?: PickState;
  awayPickState?: PickState;
  locked?: boolean;
  onPickHome?: () => void;
  onPickAway?: () => void;
  // Team metadata
  homeRecord?: string;
  awayRecord?: string;
  homeJerseyUrl?: string;
  awayJerseyUrl?: string;
  // Weather
  weatherDisplayValue?: string | null;
  weatherConditionId?: string | null;
  weatherTemperatureF?: number | null;
  // Postseason O/U
  isPostSeason?: boolean;
  overValue?: number | null;
  underValue?: number | null;
  overPickState?: PickState;
  underPickState?: PickState;
  overUnderLocked?: boolean;
  onPickOver?: () => void;
  onPickUnder?: () => void;
  // Score mode: who picked
  homePickers?: number;
  awayPickers?: number;
  // When the spread was first posted — undefined/null hides the line entirely
  spreadPostedAt?: string | null;
}

function formatShortDateTime(iso: string) {
  return toLocalDisplay(iso, {
    weekday: 'short', month: 'short', day: 'numeric', hour: 'numeric', minute: '2-digit',
  });
}

function statusChip(status: string | undefined, gameTime: string) {
  const isFinal = status === 'StatusFinal' || status === 'status_final';
  const isLive = status === 'StatusInProgress' || status === 'status_in_progress';

  if (isFinal) return <Chip label="Final" size="small" color="default" />;
  if (isLive) return <Chip label="Live" size="small" color="success" />;
  return (
    <Typography variant="caption" color="text.secondary">
      {formatShortDateTime(gameTime)}
    </Typography>
  );
}


export default function GameCard({
  homeTeam, awayTeam, homeSpread, awaySpread, gameTime,
  mode,
  homeScore, awayScore, gameStatus, gameDetail,
  homePickState = 'none', awayPickState = 'none',
  locked = false,
  onPickHome, onPickAway,
  homeRecord, awayRecord,
  homeJerseyUrl, awayJerseyUrl,
  weatherDisplayValue, weatherConditionId, weatherTemperatureF,
  isPostSeason = false,
  overValue, underValue,
  overPickState = 'none', underPickState = 'none',
  overUnderLocked = false,
  onPickOver, onPickUnder,
  homePickers, awayPickers,
  spreadPostedAt,
}: GameCardProps) {
  const isFinal = gameStatus === 'StatusFinal' || gameStatus === 'status_final';
  const isLive = gameStatus === 'StatusInProgress' || gameStatus === 'status_in_progress';
  const showScore = mode === 'score' && (isFinal || isLive);

  // frizat: "Picked" (with its checkmark icon) renders ~29px wider than "Pick" at minWidth alone
  // — since the button sits after the spread value in a right-hugging cluster, that width
  // difference shifted the value's position between a submitted row and an unsubmitted one. A
  // fixed `width` (not just minWidth) keeps every pick-state button identical, so the value's
  // position never depends on which state a given row happens to be in.
  const pickButtonSx = { width: 112, height: 44, textTransform: 'none', fontSize: '0.85rem', flexShrink: 0 } as const;

  // frizat: a rigid fixed-column grid (60/56/80/1fr) made row alignment robust but pinned the
  // spread value at a fixed offset near the left edge, with a large dead gap before the button —
  // it read as "left-aligned," not centered, and looked nothing like ScoresPage's own team rows
  // (a completely separate hand-built layout ScoresPage.tsx uses — GameCard is only ever rendered
  // in pick mode, never score mode, in the live app). ScoresPage's trick is simpler and more
  // robust: a flexGrow spacer between the left cluster (logo/record) and the right cluster
  // (value/action) — the left cluster can vary in width (record present or not) without ever
  // shifting the right cluster, which always hugs the right edge. The value itself stays
  // dead-centered within its own fixed-width box, matching what "aligned in the middle" means for
  // the number itself.
  const spreadValueSx = { minWidth: 64, textAlign: 'center', flexShrink: 0 } as const;

  // MUI's `disabled` state flattens contained (filled) buttons to uniform gray regardless of
  // `color`. Keep the real color at reduced opacity instead of falling back to generic gray.
  // frizat: warning/amber was hard to read in both light and dark mode — "available to pick"
  // uses info (blue) instead, filled the same as the picked (success/green) state so both read
  // as equally solid, deliberate choices rather than one looking washed out.
  const lockedFillSx = (color: 'success' | 'info') => ({
    '&.Mui-disabled': {
      color: `${color}.contrastText`,
      backgroundColor: `${color}.main`,
      opacity: 0.6,
    },
  });

  const renderPickButton = (team: string, pickState: PickState, onPick?: () => void) => {
    if (pickState === 'submitted')
      // aria-label stays "locked in" for accessible-name test/query stability — only the visible
      // label changed (matches the pending-state "Picked" text; disabled + checkmark still
      // distinguish submitted from pending for sighted users).
      return <Button color="success" variant="contained" disabled startIcon={<CheckIcon />} aria-label={`${team} locked in`} sx={[pickButtonSx, lockedFillSx('success')]}>Picked</Button>;
    if (pickState !== 'none')
      return <Button color="success" variant="contained" onClick={onPick} aria-label={`${team} picked`} sx={pickButtonSx}>Picked</Button>;
    return <Button color="info" variant="contained" disabled={locked} onClick={onPick} aria-label={`Pick ${team}`} sx={[pickButtonSx, lockedFillSx('info')]}>Pick</Button>;
  };

  const renderOverUnderButton = (label: string, value: number, pickState: PickState, onPick?: () => void) => {
    const picked = pickState !== 'none';
    const color = picked ? 'success' : 'info';
    return (
      // frizat: doesn't share pickButtonSx's fixed width — "Over 45.5 ✓" is much longer than
      // "Picked" and would overflow/clip a box sized for the shorter label. This row balances via
      // justifyContent="space-between" across 3 always-present items instead, so it doesn't need
      // a fixed button width to stay readable.
      <Button
        variant="contained"
        color={color}
        sx={[{ minWidth: 80, height: 44, textTransform: 'none', fontSize: '0.85rem', flexShrink: 0 }, lockedFillSx(color)]}
        disabled={overUnderLocked && !picked}
        onClick={onPick}
      >
        {picked ? `${label} ${value} ✓` : label}
      </Button>
    );
  };

  const renderTeamLogo = (abbr: string, jerseyUrl: string | undefined) => (
    <Box textAlign="center">
      {jerseyUrl
        ? <img src={jerseyUrl} width={50} alt={abbr} />
        : <TeamHelmet abbr={abbr} size={50} showLabel={false} />}
      <Typography variant="caption" display="block" sx={{ lineHeight: 1.2, opacity: 0.85 }}>
        {abbr}
      </Typography>
    </Box>
  );

  return (
    <Card elevation={2} sx={{ height: '100%' }}>
      <CardContent sx={{ pb: '12px !important' }}>
        {/* Away team row */}
        <Card variant="outlined" sx={{ mb: 1.5 }}>
          <CardContent sx={{ p: '12px !important' }}>
            <Stack direction="row" alignItems="center" sx={{ gap: 1 }}>
              {renderTeamLogo(awayTeam, awayJerseyUrl)}
              {!isPostSeason && awayRecord && (
                <Typography variant="subtitle2">{awayRecord}</Typography>
              )}
              <Box sx={{ flexGrow: 1 }} />
              <Typography variant="h6" sx={spreadValueSx}>
                {mode === 'score' && showScore ? awayScore : awaySpread != null ? spreadLabel(awaySpread) : ""}
              </Typography>
              {mode === 'pick' ? renderPickButton(awayTeam, awayPickState, onPickAway) : (
                mode === 'score' && (
                  <Typography variant="caption" color="text.secondary">
                    {awaySpread != null ? spreadLabel(awaySpread) : ""}
                    {awayPickers !== undefined && ` · ${awayPickers}👤`}
                  </Typography>
                )
              )}
            </Stack>
          </CardContent>
        </Card>

        {/* Middle: weather + status/detail */}
        <Stack direction="row" alignItems="center" justifyContent="space-between" sx={{ mb: 1.5, gap: 1 }}>
          {weatherDisplayValue ? (
            <WeatherIcon
              iconKey={weatherDisplayValue}
              conditionId={weatherConditionId}
              temperatureF={weatherTemperatureF}
              showTemp
            />
          ) : (
            <Box />
          )}
          <Box>
            {gameDetail ? (
              <Typography variant="body2" sx={{ opacity: 0.9 }}>{gameDetail}</Typography>
            ) : (
              statusChip(gameStatus, gameTime)
            )}
          </Box>
        </Stack>

        {/* Home team row */}
        <Card variant="outlined">
          <CardContent sx={{ p: '12px !important' }}>
            <Stack direction="row" alignItems="center" sx={{ gap: 1 }}>
              {renderTeamLogo(homeTeam, homeJerseyUrl)}
              {!isPostSeason && homeRecord && (
                <Typography variant="subtitle2">{homeRecord}</Typography>
              )}
              <Box sx={{ flexGrow: 1 }} />
              <Typography variant="h6" sx={spreadValueSx}>
                {mode === 'score' && showScore ? homeScore : homeSpread != null ? spreadLabel(homeSpread) : ""}
              </Typography>
              {mode === 'pick' ? renderPickButton(homeTeam, homePickState, onPickHome) : (
                mode === 'score' && (
                  <Typography variant="caption" color="text.secondary">
                    {homeSpread != null ? spreadLabel(homeSpread) : ""}
                    {homePickers !== undefined && ` · ${homePickers}👤`}
                  </Typography>
                )
              )}
            </Stack>
          </CardContent>
        </Card>

        {spreadPostedAt && (
          <Typography variant="caption" color="text.secondary" display="block" sx={{ textAlign: 'center', mt: 0.75 }}>
            Line posted {formatShortDateTime(spreadPostedAt)}
          </Typography>
        )}

        {/* Postseason O/U picks */}
        {isPostSeason && mode === 'pick' && overValue != null && underValue != null && (
          <Card
            data-testid="over-under-controls"
            variant="outlined"
            sx={{ mt: 1, p: 0 }}
          >
            <CardContent sx={{ p: '12px !important' }}>
              {/* Mirrors ScoresPage's own O/U row (button — value — button, evenly spaced) —
                  three always-present items balance naturally without needing a spacer. */}
              <Stack direction="row" alignItems="center" justifyContent="space-between" sx={{ gap: 1 }}>
                {renderOverUnderButton('Over', overValue, overPickState, onPickOver)}
                <Typography variant="h6" sx={spreadValueSx}>
                  {overValue}
                </Typography>
                {renderOverUnderButton('Under', underValue, underPickState, onPickUnder)}
              </Stack>
            </CardContent>
          </Card>
        )}
      </CardContent>
    </Card>
  );
}
