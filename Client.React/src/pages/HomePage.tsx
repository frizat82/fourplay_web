import { Box, Button, Chip, Container, Dialog, DialogContent, DialogTitle, Grid, IconButton, Paper, Stack, Typography } from '@mui/material';
import CheckIcon from '@mui/icons-material/Check';
import CloseIcon from '@mui/icons-material/Close';
import SportsTennisIcon from '@mui/icons-material/SportsTennis';
import LeaderboardIcon from '@mui/icons-material/Leaderboard';
import PersonAddIcon from '@mui/icons-material/PersonAdd';
import EmojiEventsIcon from '@mui/icons-material/EmojiEvents';
import { Link as RouterLink } from 'react-router-dom';
import { useState } from 'react';
import { useAuth } from '../services/auth';
import { useSportContext } from '../services/sport';
import { RulesContent } from './RulesPage';
import DashboardStandings from '../components/DashboardStandings';
import OwnerCostSummary from '../components/OwnerCostSummary';
import type { SportAdapter } from '../services/sportAdapter';
import './home.css';

const fantasyPains = [
  '3-hour draft night (every single year)',
  'Weekly lineup stress all season',
  'Dead when your QB gets hurt',
  'Complicated scoring nobody understands',
  'Over when your team folds in Week 8',
];

const fourplayWins = [
  'No draft, ever',
  'Pick your games, you\'re done',
  'You pick every game, every week',
  'Win the spread = point. Simple.',
  'Goes straight to the Super Bowl',
];

const heroBullets = [
  'No season-long roster to manage',
  'No injuries tanking your lineup',
  'Picks open Monday, lock at kickoff — 10 minutes a week',
  "You're competing against real friends, not a fake team",
];

interface HomePageProps {
  // Only present when rendered at /dashboard (authenticated); the public "/" route has no
  // sport-specific data to show, so DashboardStandings never renders there.
  adapter?: SportAdapter;
}

export default function HomePage({ adapter }: HomePageProps) {
  const { user } = useAuth();
  const { isCfb } = useSportContext();
  const isAuthed = Boolean(user);
  const [rulesOpen, setRulesOpen] = useState(false);

  return (
    <div>
      <Container maxWidth={false} className="hero-section" sx={{ py: 6 }}>
        <Container maxWidth="lg" className="hero-content">
          {!isAuthed && (
            <Stack direction="row" spacing={1.5} justifyContent="flex-end" sx={{ mb: 2 }}>
              <Button variant="text" component={RouterLink} to="/account/login" className="hero-auth-link">
                Login
              </Button>
              <Button variant="outlined" component={RouterLink} to="/account/register" className="hero-auth-link">
                Register
              </Button>
            </Stack>
          )}
          <Grid container spacing={4} alignItems="center">
            <Grid size={{ xs: 12, md: 6 }} className="hero-text-section">
              <Box className="hero-logo">
                <img src="/Images/retro_logo.png" alt="IV League Logo" className="hero-logo-img" />
              </Box>
              <Box className="hero-text-inner">
                <Chip
                  label={isCfb ? 'College Football' : 'NFL'}
                  color="secondary"
                  sx={{ fontWeight: 700, fontSize: '1rem', letterSpacing: '0.04em', height: 36, px: 1, mb: 1.5 }}
                />
                <Typography variant="h2" className="hero-title">
                  {isAuthed ? 'Welcome Back.' : 'Skip the Draft.\nMake Picks.\nBeat Your Friends.'}
                </Typography>
                <Typography variant="h6" className="hero-subtitle">
                  {isAuthed
                    ? 'Your picks are waiting. Check the leaderboard and see where you stand.'
                    : `IV League is what fantasy football should have been — no draft, no waiver wire, no dead lineups. Pick ${isCfb ? 'college football' : 'NFL'} games against the spread each week and watch the leaderboard.`}
                </Typography>
                {!isAuthed && (
                  <Stack spacing={1} sx={{ mb: 3 }}>
                    {heroBullets.map(b => (
                      <Stack key={b} direction="row" alignItems="flex-start" spacing={1}>
                        <CheckIcon color="secondary" fontSize="small" sx={{ mt: '3px', flexShrink: 0 }} />
                        <Typography variant="body2" className="hero-subtitle">{b}</Typography>
                      </Stack>
                    ))}
                  </Stack>
                )}
                <Stack direction={{ xs: 'column', sm: 'row' }} spacing={2} className="hero-buttons">
                  <Button
                    variant="contained"
                    size="large"
                    color="secondary"
                    className="hero-primary-btn"
                    startIcon={isAuthed ? <SportsTennisIcon /> : <PersonAddIcon />}
                    component={RouterLink}
                    to={isAuthed ? '/picks' : '/account/register'}
                  >
                    {isAuthed ? 'Make Picks' : 'Register with Invite'}
                  </Button>
                  <Button
                    variant="outlined"
                    size="large"
                    className="hero-secondary-btn"
                    startIcon={<LeaderboardIcon />}
                    {...(isAuthed
                      ? { component: RouterLink, to: '/leaderboard' }
                      : { onClick: () => document.getElementById('how-it-works')?.scrollIntoView({ behavior: 'smooth' }) }
                    )}
                  >
                    {isAuthed ? 'View Standings' : 'See How It Works ↓'}
                  </Button>
                </Stack>
              </Box>
            </Grid>
            <Grid size={{ xs: 12, md: 6 }} className="hero-image-section">
              <Stack spacing={2}>
                {!isAuthed && (
                  <Paper elevation={8} sx={{ position: 'relative', overflow: 'hidden', borderRadius: 2 }}>
                    <video
                      autoPlay
                      muted
                      loop
                      playsInline
                      controls
                      style={{ width: '100%', display: 'block' }}
                      poster="/Images/fourplayhome.jpg"
                    >
                      <source src="/Videos/demo.mp4" type="video/mp4" />
                    </video>
                  </Paper>
                )}
                <Paper className="hero-image" elevation={8}>
                  <img src="/Images/fourplayhome.jpg" alt="IV League" className="hero-image-img" />
                </Paper>
                {isAuthed && adapter && <DashboardStandings adapter={adapter} />}
                {isAuthed && <OwnerCostSummary />}
              </Stack>
            </Grid>
          </Grid>
        </Container>
      </Container>

      {!isAuthed && (
        <>
          <Container maxWidth="lg" sx={{ my: 8 }}>
            <Typography variant="h4" align="center" fontWeight={700} sx={{ mb: 1 }}>
              Fantasy Is Complicated. This Isn't.
            </Typography>
            <Typography variant="subtitle1" align="center" color="text.secondary" sx={{ mb: 5 }}>
              You've been on a fantasy team that fell apart by Week 6. IV League goes all the way to the Super Bowl.
            </Typography>
            <Grid container spacing={3}>
              <Grid size={{ xs: 12, md: 6 }}>
                <Paper elevation={1} sx={{ p: 3, borderRadius: 2, border: '1px solid', borderColor: 'divider', height: '100%' }}>
                  <Typography variant="h6" color="text.secondary" sx={{ mb: 2.5 }}>Fantasy Football</Typography>
                  <Stack spacing={1.5}>
                    {fantasyPains.map(item => (
                      <Stack key={item} direction="row" alignItems="center" spacing={1.5}>
                        <CloseIcon color="error" fontSize="small" sx={{ flexShrink: 0 }} />
                        <Typography variant="body2" color="text.secondary">{item}</Typography>
                      </Stack>
                    ))}
                  </Stack>
                </Paper>
              </Grid>
              <Grid size={{ xs: 12, md: 6 }}>
                <Paper elevation={3} sx={{ p: 3, borderRadius: 2, border: '2px solid', borderColor: 'secondary.main', height: '100%' }}>
                  <Typography variant="h6" fontWeight={700} sx={{ mb: 2.5 }}>IV League</Typography>
                  <Stack spacing={1.5}>
                    {fourplayWins.map(item => (
                      <Stack key={item} direction="row" alignItems="center" spacing={1.5}>
                        <CheckIcon color="secondary" fontSize="small" sx={{ flexShrink: 0 }} />
                        <Typography variant="body2" fontWeight={500}>{item}</Typography>
                      </Stack>
                    ))}
                  </Stack>
                </Paper>
              </Grid>
            </Grid>
          </Container>

          <Paper elevation={0} sx={{ py: 8, bgcolor: 'background.default' }} id="how-it-works">
            <Container maxWidth="lg">
              <Typography variant="h4" align="center" fontWeight={700} sx={{ mb: 6 }}>
                How It Works
              </Typography>
              <Grid container spacing={4}>
                <Grid size={{ xs: 12, md: 4 }}>
                  <Stack alignItems="center" textAlign="center" spacing={2}>
                    <Box sx={{ p: 2, borderRadius: '50%', bgcolor: 'secondary.main', display: 'inline-flex' }}>
                      <PersonAddIcon sx={{ fontSize: 36, color: 'secondary.contrastText' }} />
                    </Box>
                    <Typography variant="h6" fontWeight={700}>1. Get Invited</Typography>
                    <Typography color="text.secondary">
                      Leagues are private. Your commissioner sends you a link — that's the only way in. No public sign-ups, no strangers in your group.
                    </Typography>
                  </Stack>
                </Grid>
                <Grid size={{ xs: 12, md: 4 }}>
                  <Stack alignItems="center" textAlign="center" spacing={2}>
                    <Box sx={{ p: 2, borderRadius: '50%', bgcolor: 'secondary.main', display: 'inline-flex' }}>
                      <SportsTennisIcon sx={{ fontSize: 36, color: 'secondary.contrastText' }} />
                    </Box>
                    <Typography variant="h6" fontWeight={700}>2. Pick Against the Spread</Typography>
                    <Typography color="text.secondary">
                      Each week pick {isCfb ? 'college football' : 'NFL'} games — not just who wins, but who <em>covers</em>. {isCfb ? 'Ohio State -6.5' : 'Chiefs -6.5'} means they need to win by 7 or more. Same lines Vegas uses.
                    </Typography>
                  </Stack>
                </Grid>
                <Grid size={{ xs: 12, md: 4 }}>
                  <Stack alignItems="center" textAlign="center" spacing={2}>
                    <Box sx={{ p: 2, borderRadius: '50%', bgcolor: 'secondary.main', display: 'inline-flex' }}>
                      <EmojiEventsIcon sx={{ fontSize: 36, color: 'secondary.contrastText' }} />
                    </Box>
                    <Typography variant="h6" fontWeight={700}>3. Compete All Season</Typography>
                    <Typography color="text.secondary">
                      Results update live as games finish. The leaderboard tracks every week — regular season through Wild Card, Divisional, Championship, and the Super Bowl.
                    </Typography>
                  </Stack>
                </Grid>
              </Grid>
            </Container>
          </Paper>

          <Container maxWidth="md" sx={{ my: 8, textAlign: 'center' }}>
            <Paper className="cta-section" elevation={4}>
              <Typography variant="h4" className="cta-title">
                Got an Invite? You're Ready.
              </Typography>
              <Typography variant="subtitle1" className="cta-subtitle">
                IV League is private and invite-only. If someone sent you a link, register below and you're in.
              </Typography>
              <Stack direction={{ xs: 'column', sm: 'row' }} spacing={2} justifyContent="center" sx={{ mb: 2 }}>
                <Button
                  variant="contained"
                  size="large"
                  className="cta-button"
                  startIcon={<PersonAddIcon />}
                  component={RouterLink}
                  to="/account/register"
                >
                  Create Account
                </Button>
                <Button
                  variant="outlined"
                  size="large"
                  onClick={() => setRulesOpen(true)}
                >
                  Read the Full Rules →
                </Button>
              </Stack>
              <Typography variant="caption" color="text.secondary">
                No invite? Ask your league commissioner — they control who joins.
              </Typography>
            </Paper>
          </Container>
        </>
      )}

      <Dialog open={rulesOpen} onClose={() => setRulesOpen(false)} maxWidth="md" fullWidth scroll="paper">
        <DialogTitle sx={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
          How IV League Works
          <IconButton onClick={() => setRulesOpen(false)} size="small">
            <CloseIcon />
          </IconButton>
        </DialogTitle>
        <DialogContent dividers>
          <RulesContent />
        </DialogContent>
      </Dialog>
    </div>
  );
}
