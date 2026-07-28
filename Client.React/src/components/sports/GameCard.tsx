import { Box, Button, Card, CardContent, Chip, Stack, Typography } from '@mui/material';
import CheckIcon from '@mui/icons-material/Check';
import TeamHelmet from './TeamHelmet';
import WeatherIcon from '../WeatherIcon';
import { spreadLabel } from '../../utils/gameHelpers';

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
}: GameCardProps) {
  const isFinal = gameStatus === 'StatusFinal' || gameStatus === 'status_final';
  const isLive = gameStatus === 'StatusInProgress' || gameStatus === 'status_in_progress';
  const showScore = mode === 'score' && (isFinal || isLive);

  const pickButtonSx = { minWidth: 80, height: 44, textTransform: 'none', fontSize: '0.85rem', flexShrink: 0 } as const;

  const renderTeamLogo = (abbr: string, jerseyUrl: string | undefined, flipped = false) => (
    <Box textAlign="center">
      {jerseyUrl
        ? <img src={jerseyUrl} width={50} alt={abbr} />
        : <TeamHelmet abbr={abbr} size={50} flipped={flipped} />}
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
              {mode === 'pick' ? (
                awayPickState === 'submitted' ? (
                  <Button
                    color="success"
                    variant="contained"
                    disabled
                    startIcon={<CheckIcon />}
                    aria-label={`${awayTeam} locked in`}
                    sx={pickButtonSx}
                  >
                    Locked in
                  </Button>
                ) : awayPickState !== 'none' ? (
                  <Button
                    color="success"
                    variant="contained"
                    onClick={onPickAway}
                    aria-label={`${awayTeam} picked`}
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
                    aria-label={`Pick ${awayTeam}`}
                    sx={pickButtonSx}
                  >
                    Pick
                  </Button>
                )
              ) : (
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
              {renderTeamLogo(homeTeam, homeJerseyUrl, true)}
              {!isPostSeason && homeRecord && (
                <Typography variant="subtitle2">{homeRecord}</Typography>
              )}
              <Typography variant="h6" className="fixed-width">
                {mode === 'score' && showScore ? homeScore : homeSpread != null ? spreadLabel(homeSpread) : ""}
              </Typography>
              {mode === 'pick' ? (
                homePickState === 'submitted' ? (
                  <Button
                    color="success"
                    variant="contained"
                    disabled
                    startIcon={<CheckIcon />}
                    aria-label={`${homeTeam} locked in`}
                    sx={pickButtonSx}
                  >
                    Locked in
                  </Button>
                ) : homePickState !== 'none' ? (
                  <Button
                    color="success"
                    variant="contained"
                    onClick={onPickHome}
                    aria-label={`${homeTeam} picked`}
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
                    aria-label={`Pick ${homeTeam}`}
                    sx={pickButtonSx}
                  >
                    Pick
                  </Button>
                )
              ) : (
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
