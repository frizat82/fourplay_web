import { Box, Button, Container, Dialog, DialogContent, DialogTitle, Grid, IconButton, Paper, Stack, Typography } from '@mui/material';
import CheckIcon from '@mui/icons-material/Check';
import CloseIcon from '@mui/icons-material/Close';
import SportsTennisIcon from '@mui/icons-material/SportsTennis';
import LeaderboardIcon from '@mui/icons-material/Leaderboard';
import PersonAddIcon from '@mui/icons-material/PersonAdd';
import EmojiEventsIcon from '@mui/icons-material/EmojiEvents';
import VolumeOffIcon from '@mui/icons-material/VolumeOff';
import VolumeUpIcon from '@mui/icons-material/VolumeUp';
import { Link as RouterLink } from 'react-router-dom';
import { useRef, useState } from 'react';
import { useAuth } from '../services/auth';
import { RulesContent } from './RulesPage';
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

export default function HomePage() {
  const { user } = useAuth();
  const isAuthed = Boolean(user);
  const videoRef = useRef<HTMLVideoElement>(null);
  const [muted, setMuted] = useState(true);
  const [rulesOpen, setRulesOpen] = useState(false);

  const toggleMute = () => {
    if (videoRef.current) {
      videoRef.current.muted = !videoRef.current.muted;
      setMuted(videoRef.current.muted);
    }
  };

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
                  {isAuthed ? 'Welcome Back.' : 'Skip the Draft.\nMake Picks.\nBeat Your Friends.'}
                </Typography>
                <Typography variant="h6" className="hero-subtitle">
                  {isAuthed
                    ? 'Your picks are waiting. Check the leaderboard and see where you stand.'
                    : "IV League is what fantasy football should have been — no draft, no waiver wire, no dead lineups. Pick NFL games against the spread each week and watch the leaderboard."}
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
              <Paper className="hero-image" elevation={8} sx={{ position: 'relative' }}>
                <video
                  ref={videoRef}
                  autoPlay
                  muted
                  loop
                  playsInline
                  className="hero-image-img"
                  poster="/Images/fourplayhome.jpg"
                >
                  <source src="/Videos/demo.mp4" type="video/mp4" />
                  <img src="/Images/fourplayhome.jpg" alt="FourPlay" className="hero-image-img" />
                </video>
                <IconButton
                  onClick={toggleMute}
                  size="small"
                  sx={{
                    position: 'absolute',
                    bottom: 10,
                    right: 10,
                    bgcolor: 'rgba(0,0,0,0.5)',
                    color: 'white',
                    '&:hover': { bgcolor: 'rgba(0,0,0,0.75)' },
                  }}
                >
                  {muted ? <VolumeOffIcon fontSize="small" /> : <VolumeUpIcon fontSize="small" />}
                </IconButton>
              </Paper>
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
                      Each week pick NFL games — not just who wins, but who <em>covers</em>. Chiefs&nbsp;-6.5 means they need to win by 7 or more. Same lines Vegas uses.
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
