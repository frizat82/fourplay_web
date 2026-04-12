import { Box, Card, CardContent, Divider, IconButton, Stack, Typography } from '@mui/material';
import ArrowBackIcon from '@mui/icons-material/ArrowBack';
import { useNavigate } from 'react-router-dom';
import { getEspnRequiredPicks } from '../utils/gameHelpers';
import PageHeader from '../components/PageHeader';

function Section({ title, children }: { title: string; children: React.ReactNode }) {
  return (
    <Box>
      <Typography variant="h6" fontWeight={700} gutterBottom>
        {title}
      </Typography>
      <Divider sx={{ mb: 2 }} />
      {children}
    </Box>
  );
}

function Rule({ children }: { children: React.ReactNode }) {
  return (
    <Typography variant="body2" color="text.secondary" sx={{ mb: 1 }}>
      {children}
    </Typography>
  );
}

export default function RulesPage() {
  const navigate = useNavigate();
  const regularPicks = getEspnRequiredPicks(1, false);
  const wildCardPicks = getEspnRequiredPicks(1, true);
  const divisionalPicks = getEspnRequiredPicks(3, true);
  const championshipPicks = getEspnRequiredPicks(4, true);
  const superBowlPicks = getEspnRequiredPicks(5, true);

  return (
    <Card sx={{ minHeight: 'calc(100vh - 128px)' }}>
      <CardContent>
        <Box sx={{ display: 'flex', alignItems: 'flex-start', mb: 2 }}>
          <IconButton onClick={() => navigate(-1)} sx={{ mr: 1, mt: 0.5 }}>
            <ArrowBackIcon />
          </IconButton>
          <Box sx={{ flex: 1 }}>
            <PageHeader title="How FourPlay Works" subtitle="Everything you need to know before making your picks." />
          </Box>
        </Box>
        <Stack spacing={3}>

          <Section title="How Picks Work">
            <Rule>
              Each week you pick teams against the spread. You must submit{' '}
              <strong>{regularPicks} picks</strong> per week during the regular season.
            </Rule>
            <Rule>
              Pick the team you think will cover the spread — not just win the game. If the spread is
              Chiefs -6.5 and they win by 10, the Chiefs cover.
            </Rule>
            <Rule>A push (exactly on the spread) counts as a loss for your pick.</Rule>
          </Section>

          <Section title="When Games Lock">
            <Rule>
              Each game locks at its individual <strong>kickoff time</strong>. You can still make or
              change picks for later games after early games have started.
            </Rule>
            <Rule>
              For example, if the 1pm ET games have kicked off, you can still pick the 4pm ET or
              Sunday Night Football games.
            </Rule>
            <Rule>No picks are accepted after a game has started. This is enforced server-side.</Rule>
          </Section>

          <Section title="Playoff Rounds">
            <Rule>
              Playoff weeks have different required pick counts and may include Saturday games.
            </Rule>
            <Stack spacing={1} sx={{ mt: 1 }}>
              {[
                { round: 'Wild Card', picks: wildCardPicks },
                { round: 'Divisional', picks: divisionalPicks },
                { round: 'Championship', picks: championshipPicks },
                { round: 'Super Bowl', picks: superBowlPicks },
              ].map(({ round, picks }) => (
                <Box
                  key={round}
                  sx={{
                    display: 'flex',
                    justifyContent: 'space-between',
                    alignItems: 'center',
                    px: 2,
                    py: 1,
                    borderRadius: 1,
                    bgcolor: 'action.hover',
                  }}
                >
                  <Typography variant="body2" fontWeight={500}>
                    {round}
                  </Typography>
                  <Typography variant="body2" color="text.secondary">
                    {picks} {picks === 1 ? 'pick' : 'picks'}
                  </Typography>
                </Box>
              ))}
            </Stack>
          </Section>

          <Section title="Scoring">
            <Rule>
              Each correct pick earns points. The juice (vig) set per league may adjust payout — check
              your league settings for the exact rate.
            </Rule>
            <Rule>
              If you miss the deadline for a game, that pick is forfeited. Submitting fewer than the
              required picks means you lose those slots.
            </Rule>
            <Rule>The leaderboard updates as game results are confirmed.</Rule>
          </Section>
        </Stack>
      </CardContent>
    </Card>
  );
}
