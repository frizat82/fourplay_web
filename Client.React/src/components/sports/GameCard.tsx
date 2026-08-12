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

  const pickButtonSx = { minWidth: 80, height: 44, textTransform: 'none', fontSize: '0.85rem', flexShrink: 0 } as const;

  const renderPickButton = (team: string, pickState: PickState, onPick?: () => void) => {
    if (pickState === 'submitted')
      // aria-label stays "locked in" for accessible-name test/query stability — only the visible
      // label changed (matches the pending-state "Picked" text; disabled + checkmark still
      // distinguish submitted from pending for sighted users).
      return <Button color="success" variant="contained" disabled startIcon={<CheckIcon />} aria-label={`${team} locked in`} sx={pickButtonSx}>Picked</Button>;
    if (pickState !== 'none')
      return <Button color="success" variant="contained" onClick={onPick} aria-label={`${team} picked`} sx={pickButtonSx}>Picked</Button>;
    return <Button color="warning" variant="contained" disabled={locked} onClick={onPick} aria-label={`Pick ${team}`} sx={pickButtonSx}>Pick</Button>;
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
            <Stack direction="row" alignItems="center" justifyContent="space-between">
              {renderTeamLogo(awayTeam, awayJerseyUrl)}
              {!isPostSeason && awayRecord && (
                <Typography variant="subtitle2">{awayRecord}</Typography>
              )}
              <Typography variant="h6" className="fixed-width">
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
            <Stack direction="row" alignItems="center" justifyContent="space-between">
              {renderTeamLogo(homeTeam, homeJerseyUrl)}
              {!isPostSeason && homeRecord && (
                <Typography variant="subtitle2">{homeRecord}</Typography>
              )}
              <Typography variant="h6" className="fixed-width">
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
              <Stack direction="row" alignItems="center" justifyContent="space-between" sx={{ gap: 1 }}>
                <Button
                  variant="contained"
                  color={overPickState !== 'none' ? 'success' : 'warning'}
                  sx={pickButtonSx}
                  disabled={overUnderLocked && overPickState === 'none'}
                  onClick={onPickOver}
                >
                  {overPickState !== 'none' ? `Over ${overValue} ✓` : 'Over'}
                </Button>
                <Typography variant="h6" className="fixed-width" sx={{ textAlign: 'center', flexShrink: 0 }}>
                  {overValue}
                </Typography>
                <Button
                  variant="contained"
                  color={underPickState !== 'none' ? 'success' : 'warning'}
                  sx={pickButtonSx}
                  disabled={overUnderLocked && underPickState === 'none'}
                  onClick={onPickUnder}
                >
                  {underPickState !== 'none' ? `Under ${underValue} ✓` : 'Under'}
                </Button>
              </Stack>
            </CardContent>
          </Card>
        )}
      </CardContent>
    </Card>
  );
}
