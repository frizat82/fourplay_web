import { Box, Button, Container, Grid, Paper, Stack, Typography } from '@mui/material';
import SportsTennisIcon from '@mui/icons-material/SportsTennis';
import LeaderboardIcon from '@mui/icons-material/Leaderboard';
import TimelineIcon from '@mui/icons-material/Timeline';
import GroupIcon from '@mui/icons-material/Group';
import AnalyticsIcon from '@mui/icons-material/Analytics';
import PlayArrowIcon from '@mui/icons-material/PlayArrow';
import { Link as RouterLink } from 'react-router-dom';
import { useAuth } from '../services/auth';
import './home.css';

export default function HomePage() {
  const { user } = useAuth();
  const isAuthed = Boolean(user);

  return (
    <div>
      <Container maxWidth={false} className="hero-section" sx={{ py: 6 }}>
        <Container maxWidth="lg" className="hero-content">
          <Stack direction="row" spacing={1.5} justifyContent="flex-end" sx={{ mb: 2 }}>
            <Button variant="text" component={RouterLink} to="/account/login" className="hero-auth-link">
              {isAuthed ? 'Switch Account' : 'Login'}
            </Button>
            {!isAuthed && (
              <Button variant="outlined" component={RouterLink} to="/account/register" className="hero-auth-link">
                Register
              </Button>
            )}
          </Stack>
          <Grid container spacing={4} alignItems="center">
            <Grid size={{ xs: 12, md: 6 }} className="hero-text-section">
              <Box className="hero-logo">
                <img src="/Images/retro_logo.png" alt="FourPlay Logo" className="hero-logo-img" />
              </Box>
              <Box className="hero-text-inner">
                <Typography variant="h2" className="hero-title">
                  Elevate Your Fantasy Game
                </Typography>
                <Typography variant="h6" className="hero-subtitle">
                  Join the ultimate fantasy sports community. Make picks, climb leaderboards,
                  and compete with friends across multiple leagues.
                </Typography>
                <Stack direction={{ xs: 'column', sm: 'row' }} spacing={2} className="hero-buttons">
                  <Button
                    variant="contained"
                    size="large"
                    color="secondary"
                    className="hero-primary-btn"
                    startIcon={<SportsTennisIcon />}
                    component={RouterLink}
                    to={isAuthed ? '/picks' : '/account/login?returnUrl=%2Fpicks'}
                  >
                    {isAuthed ? 'Make Picks' : 'Log In to Start'}
                  </Button>
                  <Button
                    variant="outlined"
                    size="large"
                    className="hero-secondary-btn"
                    startIcon={<LeaderboardIcon />}
                    component={RouterLink}
                    to={isAuthed ? '/leaderboard' : '/account/register'}
                  >
                    {isAuthed ? 'View Standings' : 'Create Account'}
                  </Button>
                </Stack>
              </Box>
            </Grid>
            <Grid size={{ xs: 12, md: 6 }} className="hero-image-section">
              <Paper className="hero-image" elevation={8}>
                <img src="/Images/fourplayhome.jpg" alt="FourPlay Fantasy Sports" className="hero-image-img" />
              </Paper>
            </Grid>
          </Grid>
        </Container>
      </Container>

      <Container maxWidth="lg" sx={{ my: 8 }}>
        <Typography variant="h4" align="center" sx={{ mb: 4 }}>
          Why Choose FourPlay?
        </Typography>
        <Grid container spacing={4}>
          <Grid size={{ xs: 12, md: 4 }}>
            <Paper className="feature-card" elevation={3}>
              <TimelineIcon color="primary" fontSize="large" />
              <Typography variant="h6" sx={{ mt: 2 }}>
                Live Scoring
              </Typography>
              <Typography color="text.secondary">
                Track your picks in real-time with live game updates and instant scoring.
              </Typography>
            </Paper>
          </Grid>
          <Grid size={{ xs: 12, md: 4 }}>
            <Paper className="feature-card" elevation={3}>
              <GroupIcon color="secondary" fontSize="large" />
              <Typography variant="h6" sx={{ mt: 2 }}>
                League Competition
              </Typography>
              <Typography color="text.secondary">
                Create or join leagues with friends and compete for the top spot.
              </Typography>
            </Paper>
          </Grid>
          <Grid size={{ xs: 12, md: 4 }}>
            <Paper className="feature-card" elevation={3}>
              <AnalyticsIcon color="primary" fontSize="large" />
              <Typography variant="h6" sx={{ mt: 2 }}>
                Advanced Stats
              </Typography>
              <Typography color="text.secondary">
                Analyze your performance with detailed statistics and trending data.
              </Typography>
            </Paper>
          </Grid>
        </Grid>
      </Container>

      <Paper className="stats-section" elevation={2}>
        <Container maxWidth="lg" sx={{ py: 6 }}>
          <Grid container spacing={4} justifyContent="space-around" textAlign="center">
            <Grid size={{ xs: 6, md: 3 }}>
              <Typography variant="h4" className="stats-number">
                500+
              </Typography>
              <Typography color="text.secondary">Active Players</Typography>
            </Grid>
            <Grid size={{ xs: 6, md: 3 }}>
              <Typography variant="h4" className="stats-number">
                25+
              </Typography>
              <Typography color="text.secondary">Active Leagues</Typography>
            </Grid>
            <Grid size={{ xs: 6, md: 3 }}>
              <Typography variant="h4" className="stats-number">
                10K+
              </Typography>
              <Typography color="text.secondary">Picks Made</Typography>
            </Grid>
            <Grid size={{ xs: 6, md: 3 }}>
              <Typography variant="h4" className="stats-number">
                24/7
              </Typography>
              <Typography color="text.secondary">Live Updates</Typography>
            </Grid>
          </Grid>
        </Container>
      </Paper>

      <Container maxWidth="md" sx={{ my: 8, textAlign: 'center' }}>
        <Paper className="cta-section" elevation={4}>
          <Typography variant="h4" className="cta-title">
            Ready to Start Playing?
          </Typography>
          <Typography variant="subtitle1" className="cta-subtitle">
            Join thousands of fantasy sports enthusiasts making winning picks every week.
          </Typography>
          <Button
            variant="contained"
            size="large"
            className="cta-button"
            startIcon={<PlayArrowIcon />}
            component={RouterLink}
            to={isAuthed ? '/dashboard' : '/account/login?returnUrl=%2Fdashboard'}
          >
            {isAuthed ? 'Open Dashboard' : 'Get Started Now'}
          </Button>
        </Paper>
      </Container>
    </div>
  );
}
