import {
  Box,
  Card,
  CardContent,
  Chip,
  Divider,
  Grid,
  Paper,
  Stack,
  Typography,
} from '@mui/material';
import { getEspnRequiredPicks } from '../utils/gameHelpers';
import PageHeader from '../components/PageHeader';

function SectionLabel({ children }: { children: React.ReactNode }) {
  return (
    <Typography
      variant="overline"
      sx={{ color: 'text.disabled', letterSpacing: '0.08em', mb: 1, display: 'block' }}
    >
      {children}
    </Typography>
  );
}

function RuleRow({
  color,
  children,
}: {
  color: 'primary' | 'secondary' | 'success' | 'error' | 'warning' | 'info';
  children: React.ReactNode;
}) {
  return (
    <Box
      sx={{
        display: 'flex',
        gap: 1.5,
        alignItems: 'flex-start',
        py: 1,
        '&:not(:last-child)': { borderBottom: '1px solid', borderColor: 'divider' },
      }}
    >
      <Box
        sx={{
          width: 24,
          height: 24,
          borderRadius: '50%',
          bgcolor: `${color}.main`,
          opacity: 0.15,
          flexShrink: 0,
          mt: 0.25,
        }}
      />
      <Typography variant="body2" color="text.secondary" sx={{ lineHeight: 1.7 }}>
        {children}
      </Typography>
    </Box>
  );
}

function TeaseFormula() {
  return (
    <Box
      sx={{
        display: 'flex',
        alignItems: 'center',
        justifyContent: 'center',
        gap: 1,
        py: 1.5,
        flexWrap: 'wrap',
      }}
    >
      <Chip label="Vegas line" variant="outlined" size="small" />
      <Typography color="text.disabled" fontWeight={500}>
        +
      </Typography>
      <Chip label="13 pt tease" size="small" color="secondary" variant="outlined" />
      <Typography color="text.disabled" fontWeight={500}>
        =
      </Typography>
      <Chip label="Your line" size="small" color="success" />
    </Box>
  );
}

function MatchupExample() {
  const teams = [
    { name: 'Seattle Seahawks', detail: 'Home favorite', vegas: '−4.5', teased: '+8.5' },
    { name: 'Chicago Bears', detail: 'Road underdog', vegas: '+4.5', teased: '+17.5' },
  ];

  return (
    <Paper variant="outlined" sx={{ overflow: 'hidden', borderRadius: 2 }}>
      <Box
        sx={{
          px: 2,
          py: 1,
          bgcolor: 'action.hover',
          borderBottom: '1px solid',
          borderColor: 'divider',
          display: 'flex',
          justifyContent: 'space-between',
          alignItems: 'center',
        }}
      >
        <Typography
          variant="caption"
          color="text.disabled"
          sx={{ letterSpacing: '0.06em', textTransform: 'uppercase' }}
        >
          Week 1 · 2026 · SEA hosts CHI
        </Typography>
        <Typography variant="caption" color="text.disabled">
          Vegas → Teased
        </Typography>
      </Box>
      {teams.map((team, i) => (
        <Box
          key={team.name}
          sx={{
            display: 'grid',
            gridTemplateColumns: '1fr auto auto',
            gap: 1,
            alignItems: 'center',
            px: 2,
            py: 1.25,
            borderBottom: i === 0 ? '1px solid' : 'none',
            borderColor: 'divider',
          }}
        >
          <Box>
            <Typography variant="body2" fontWeight={500}>
              {team.name}
            </Typography>
            <Typography variant="caption" color="text.disabled">
              {team.detail}
            </Typography>
          </Box>
          <Typography
            variant="body2"
            color="text.disabled"
            sx={{ textDecoration: 'line-through' }}
          >
            {team.vegas}
          </Typography>
          <Typography variant="body2" fontWeight={500} color="success.main">
            {team.teased}
          </Typography>
        </Box>
      ))}
    </Paper>
  );
}

function ScenarioExample() {
  const rows = [
    {
      team: 'Seattle Seahawks',
      detail: 'Teased line: +8.5 · Lost by 10',
      score: 17,
      result: 'Loser' as const,
    },
    {
      team: 'Chicago Bears',
      detail: 'Teased line: +17.5 · Won outright',
      score: 27,
      result: 'Winner' as const,
    },
  ];

  return (
    <Paper variant="outlined" sx={{ overflow: 'hidden', borderRadius: 2 }}>
      <Box
        sx={{
          px: 2,
          py: 1,
          bgcolor: 'action.hover',
          borderBottom: '1px solid',
          borderColor: 'divider',
        }}
      >
        <Typography variant="body2" fontWeight={500}>
          Final: Bears 27, Seahawks 17 &nbsp;·&nbsp; Bears win by 10
        </Typography>
      </Box>
      {rows.map((row, i) => (
        <Box
          key={row.team}
          sx={{
            display: 'grid',
            gridTemplateColumns: '1fr auto auto',
            gap: 1,
            alignItems: 'center',
            px: 2,
            py: 1.25,
            borderBottom: i === 0 ? '1px solid' : 'none',
            borderColor: 'divider',
          }}
        >
          <Box>
            <Typography variant="body2" fontWeight={500}>
              {row.team}
            </Typography>
            <Typography variant="caption" color="text.disabled">
              {row.detail}
            </Typography>
          </Box>
          <Typography variant="h6" fontWeight={500}>
            {row.score}
          </Typography>
          <Chip
            label={row.result}
            size="small"
            color={row.result === 'Winner' ? 'success' : 'error'}
            sx={{ fontWeight: 600, minWidth: 64 }}
          />
        </Box>
      ))}
    </Paper>
  );
}

function PlayoffGrid() {
  const wildCardPicks = getEspnRequiredPicks(1, true);
  const divisionalPicks = getEspnRequiredPicks(3, true);
  const championshipPicks = getEspnRequiredPicks(4, true);
  const superBowlPicks = getEspnRequiredPicks(5, true);

  const rounds = [
    { round: 'Wild Card', picks: wildCardPicks },
    { round: 'Divisional', picks: divisionalPicks },
    { round: 'Championship', picks: championshipPicks },
    { round: 'Super Bowl', picks: superBowlPicks },
  ];

  return (
    <Grid container spacing={1}>
      {rounds.map(({ round, picks }) => (
        <Grid size={6} key={round}>
          <Box sx={{ bgcolor: 'action.hover', borderRadius: 1.5, p: 1.5 }}>
            <Typography variant="caption" color="text.secondary" display="block">
              {round}
            </Typography>
            <Typography variant="h5" fontWeight={500}>
              {picks}
            </Typography>
            <Typography variant="caption" color="text.disabled">
              {picks === 1 ? 'pick required' : 'picks required'}
            </Typography>
          </Box>
        </Grid>
      ))}
    </Grid>
  );
}

function InfoCallout({ children }: { children: React.ReactNode }) {
  return (
    <Box
      sx={{
        borderLeft: '2px solid',
        borderColor: 'info.main',
        backgroundColor: (t) =>
          t.palette.mode === 'dark' ? 'rgba(59,130,246,0.1)' : 'rgba(59,130,246,0.06)',
        borderRadius: '0 8px 8px 0',
        px: 1.5,
        py: 1,
        mt: 1,
      }}
    >
      <Typography variant="caption" color="info.main" sx={{ lineHeight: 1.6 }}>
        {children}
      </Typography>
    </Box>
  );
}

export function RulesContent() {
  const regularPicks = getEspnRequiredPicks(1, false);

  return (
    <Stack spacing={3}>
      {/* The Tease */}
      <Box>
        <SectionLabel>The tease — the whole game</SectionLabel>
        <Box sx={{ bgcolor: 'action.hover', borderRadius: 2, p: 2, mb: 1 }}>
          <Typography variant="body2" color="text.secondary">
            Every Vegas line is teased <strong>13 points</strong> in your favor. This moves the
            spread dramatically — but you still have to get all{' '}
            <strong>{regularPicks} picks right</strong> to win the week.
          </Typography>
          <TeaseFormula />
        </Box>
        <MatchupExample />
        <InfoCallout>
          You can pick <strong>both sides</strong> of the same game. Expect a close game? Take
          Seattle +8.5 and Chicago +17.5 — that&apos;s two of your four picks from one matchup.
        </InfoCallout>
      </Box>

      <Divider />

      {/* Live Example */}
      <Box>
        <SectionLabel>Live example — final score</SectionLabel>
        <ScenarioExample />
        <InfoCallout>
          Seattle lost by 10 — not enough to cover their teased line of +8.5. Chicago won the game
          outright by 10, easily covering their teased line of +17.5.
        </InfoCallout>
      </Box>

      <Divider />

      {/* How Picks Work */}
      <Box>
        <SectionLabel>How picks work</SectionLabel>
        <Paper variant="outlined" sx={{ borderRadius: 2, p: 1.5 }}>
          <RuleRow color="primary">
            Each week you pick <strong>{regularPicks} teams</strong> against the teased spread
            during the regular season.
          </RuleRow>
          <RuleRow color="secondary">
            A <strong>push</strong> (team covers exactly) counts as a loss for your pick.
          </RuleRow>
          <RuleRow color="error">
            Missing the deadline for a game means that pick is <strong>forfeited</strong> — you
            lose the slot.
          </RuleRow>
        </Paper>
      </Box>

      <Divider />

      {/* Scoring & Juice */}
      <Box>
        <SectionLabel>Scoring &amp; the juice</SectionLabel>
        <Paper variant="outlined" sx={{ borderRadius: 2, p: 1.5 }}>
          <RuleRow color="success">
            Win the week and you <strong>earn the juice</strong> from that week. Juice accumulates
            across all weeks and you settle up with your league at the end of the year.
          </RuleRow>
          <RuleRow color="error">
            Miss <strong>any one</strong> of your four picks and you owe the winners the juice —
            e.g. 5 points per loss.
          </RuleRow>
          <RuleRow color="info">
            Each week you either win or owe juice — it all adds up. The leaderboard updates as
            game results are confirmed.
          </RuleRow>
          <RuleRow color="warning">
            The juice rate is set per league — check your league settings for the exact amount.
          </RuleRow>
        </Paper>
      </Box>

      <Divider />

      {/* When Games Lock */}
      <Box>
        <SectionLabel>When games lock</SectionLabel>
        <Paper variant="outlined" sx={{ borderRadius: 2, p: 1.5 }}>
          <RuleRow color="info">
            Each game locks at its individual <strong>kickoff time</strong> — not all at once. Miss
            one window, you can still pick later games.
          </RuleRow>
          <RuleRow color="primary">
            If 1pm ET games have kicked off, you can still pick the 4pm ET or Sunday Night Football
            games.
          </RuleRow>
          <RuleRow color="error">
            No picks accepted after a game starts. This is <strong>enforced server-side</strong> —
            no exceptions.
          </RuleRow>
        </Paper>
        <InfoCallout>
          Tip: submit your picks early in the week to avoid missing late lineup changes or injuries
          that shift the spread.
        </InfoCallout>
      </Box>

      <Divider />

      {/* Playoff Rounds */}
      <Box>
        <SectionLabel>Playoff picks required</SectionLabel>
        <Typography variant="body2" color="text.secondary" sx={{ mb: 1.5 }}>
          Fewer games each round means fewer required picks.
        </Typography>
        <PlayoffGrid />
      </Box>
    </Stack>
  );
}

export default function RulesPage() {
  return (
    <Card sx={{ minHeight: 'calc(100vh - 128px)' }}>
      <CardContent>
        <Box sx={{ mb: 2 }}>
          <PageHeader
            title="How FourPlay Works"
            subtitle="Pick four teased lines each week. Sounds easy — it isn't."
          />
        </Box>
        <RulesContent />
      </CardContent>
    </Card>
  );
}
