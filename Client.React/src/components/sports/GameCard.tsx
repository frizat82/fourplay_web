import { Box, Button, Card, CardContent, Chip, Stack, Typography } from '@mui/material';
import TeamHelmet from './TeamHelmet';
import WeatherIcon from '../WeatherIcon';
import { spreadLabel } from '../../utils/gameHelpers';

export type PickState = 'none' | 'pending' | 'submitted';

export interface GameCardProps {
  homeTeam: string;
  awayTeam: string;
  homeSpread: number;
  awaySpread: number;
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
}

function statusChip(status: string | undefined, gameTime: string) {
  const isFinal = status === 'StatusFinal' || status === 'status_final';
  const isLive = status === 'StatusInProgress' || status === 'status_in_progress';

  if (isFinal) return <Chip label="Final" size="small" color="default" />;
  if (isLive) return <Chip label="Live" size="small" color="success" />;
  return (
    <Typography variant="caption" color="text.secondary">
      {new Date(gameTime).toLocaleString([], { weekday: 'short', month: 'short', day: 'numeric', hour: 'numeric', minute: '2-digit' })}
    </Typography>
  );
}

function pickButtonColor(state: PickState): 'success' | 'warning' | 'inherit' {
  if (state === 'submitted' || state === 'pending') return 'success';
  return 'warning';
}

function pickButtonLabel(state: PickState, defaultLabel: string, pickedLabel = 'Picked'): string {
  return state !== 'none' ? pickedLabel : defaultLabel;
}

export default function GameCard({
  homeTeam, awayTeam, homeSpread, awaySpread, overUnder, gameTime,
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
}: GameCardProps) {
  const isFinal = gameStatus === 'StatusFinal' || gameStatus === 'status_final';
  const isLive = gameStatus === 'StatusInProgress' || gameStatus === 'status_in_progress';
  const showScore = mode === 'score' && (isFinal || isLive);

  const pickButtonSx = { minWidth: 80, height: 44, textTransform: 'none', fontSize: '0.85rem', flexShrink: 0 } as const;

  const renderTeamLogo = (abbr: string, jerseyUrl: string | undefined, flipped = false) => {
    if (jerseyUrl) return <img src={jerseyUrl} width={50} alt={abbr} />;
    return <TeamHelmet abbr={abbr} size={50} flipped={flipped} />;
  };

  return (
    <Card elevation={2} sx={{ height: '100%' }}>
      <CardContent sx={{ pb: '12px !important' }}>
        {/* Away team row */}
        <Card variant="outlined" sx={{ mb: 1.5 }}>
          <CardContent sx={{ p: '12px !important' }}>
            <Stack direction="row" alignItems="center" justifyContent="space-between">
              <Box textAlign="center">{renderTeamLogo(awayTeam, awayJerseyUrl)}</Box>
              {!isPostSeason && awayRecord && (
                <Typography variant="subtitle2">{awayRecord}</Typography>
              )}
              <Typography variant="h6" className="fixed-width">
                {mode === 'score' && showScore ? awayScore : spreadLabel(awaySpread)}
              </Typography>
              {mode === 'pick' ? (
                awayPickState !== 'none' ? (
                  <Button
                    color="success"
                    variant="contained"
                    onClick={onPickAway}
                    sx={pickButtonSx}
                  >
                    Picked
                  </Button>
                ) : (
                  <Button
                    color="warning"
                    variant="contained"
                    disabled={locked}
                    onClick={onPickAway}
                    sx={pickButtonSx}
                  >
                    Pick
                  </Button>
                )
              ) : (
                mode === 'score' && (
                  <Typography variant="caption" color="text.secondary">
                    {spreadLabel(awaySpread)}
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
              <Box textAlign="center">{renderTeamLogo(homeTeam, homeJerseyUrl, true)}</Box>
              {!isPostSeason && homeRecord && (
                <Typography variant="subtitle2">{homeRecord}</Typography>
              )}
              <Typography variant="h6" className="fixed-width">
                {mode === 'score' && showScore ? homeScore : spreadLabel(homeSpread)}
              </Typography>
              {mode === 'pick' ? (
                homePickState !== 'none' ? (
                  <Button
                    color="success"
                    variant="contained"
                    onClick={onPickHome}
                    sx={pickButtonSx}
                  >
                    Picked
                  </Button>
                ) : (
                  <Button
                    color="warning"
                    variant="contained"
                    disabled={locked}
                    onClick={onPickHome}
                    sx={pickButtonSx}
                  >
                    Pick
                  </Button>
                )
              ) : (
                mode === 'score' && (
                  <Typography variant="caption" color="text.secondary">
                    {spreadLabel(homeSpread)}
                    {homePickers !== undefined && ` · ${homePickers}👤`}
                  </Typography>
                )
              )}
            </Stack>
          </CardContent>
        </Card>

        {/* Postseason O/U picks */}
        {isPostSeason && mode === 'pick' && overValue != null && underValue != null && (
          <Card
            data-testid="over-under-controls"
            variant="outlined"
            sx={{ mt: 1, p: 0 }}
          >
            <CardContent sx={{ p: '12px !important' }}>
              <Stack direction="row" alignItems="center" justifyContent="space-between" sx={{ gap: 0.5 }}>
                <Box textAlign="center" sx={{ minWidth: 44 }}>
                  <Typography variant="caption" sx={{ opacity: 0.7, display: 'block' }}>Over</Typography>
                  <Typography variant="caption" fontWeight={700}>{overValue}</Typography>
                </Box>
                <Button
                  variant="contained"
                  color={overPickState !== 'none' ? 'success' : 'warning'}
                  sx={{ minWidth: 76, height: 36, fontSize: '0.8rem', px: 1 }}
                  disabled={overUnderLocked && overPickState === 'none'}
                  onClick={onPickOver}
                >
                  {pickButtonLabel(overPickState, 'Over', 'Overed')}
                </Button>
                <Button
                  variant="contained"
                  color={underPickState !== 'none' ? 'success' : 'warning'}
                  sx={{ minWidth: 76, height: 36, fontSize: '0.8rem', px: 1 }}
                  disabled={overUnderLocked && underPickState === 'none'}
                  onClick={onPickUnder}
                >
                  {pickButtonLabel(underPickState, 'Under', 'Undered')}
                </Button>
                <Box textAlign="center" sx={{ minWidth: 44 }}>
                  <Typography variant="caption" sx={{ opacity: 0.7, display: 'block' }}>Under</Typography>
                  <Typography variant="caption" fontWeight={700}>{underValue}</Typography>
                </Box>
              </Stack>
            </CardContent>
          </Card>
        )}
      </CardContent>
    </Card>
  );
}
