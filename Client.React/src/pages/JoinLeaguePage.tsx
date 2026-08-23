import { useEffect, useState } from 'react';
import { useNavigate, useParams } from 'react-router';
import Box from '@mui/material/Box';
import Button from '@mui/material/Button';
import Card from '@mui/material/Card';
import CardContent from '@mui/material/CardContent';
import CircularProgress from '@mui/material/CircularProgress';
import Typography from '@mui/material/Typography';
import { validateInviteLink, joinViaLink, type LeagueInviteLinkDto } from '../api/league';
import { useAuth } from '../services/auth';
import { useSession } from '../services/session';
import CheckCircleOutlineIcon from '@mui/icons-material/CheckCircleOutline';

export default function JoinLeaguePage() {
  const { token } = useParams<{ token: string }>();
  const navigate = useNavigate();
  const { user } = useAuth();
  const { reloadLeagues, availableLeagues, refreshPendingInvites } = useSession();

  const [loading, setLoading] = useState(true);
  const [joining, setJoining] = useState(false);
  const [link, setLink] = useState<LeagueInviteLinkDto | null>(null);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    if (!token) { setLoading(false); return; }
    const controller = new AbortController();
    validateInviteLink(token).then((result) => {
      if (!controller.signal.aborted) setLink(result);
    }).catch(() => {
      if (!controller.signal.aborted) setError('Failed to load invite link. Please try again.');
    }).finally(() => {
      if (!controller.signal.aborted) setLoading(false);
    });
    return () => controller.abort();
  }, [token]);

  const handleSignUp = () => {
    navigate(`/account/register?inviteLinkToken=${token}&returnUrl=/join/${token}`);
  };

  const handleJoin = async () => {
    setJoining(true);
    setError(null);
    try {
      // joinViaLink creates a pending membership invite rather than joining directly (see
      // LeagueController.JoinViaLink) — refresh pending invites, not leagues, so
      // PendingInviteBanner shows up immediately on the dashboard with Accept/Decline.
      await joinViaLink(token!);
      await refreshPendingInvites();
      navigate('/dashboard');
    } catch (err) {
      const status = (err as { response?: { status?: number } }).response?.status;
      if (status === 409) {
        await reloadLeagues();
        navigate('/dashboard');
      } else {
        setError('Failed to join league. The link may have expired.');
      }
    } finally {
      setJoining(false);
    }
  };

  if (loading) {
    return (
      <Box sx={{ display: 'flex', justifyContent: 'center', mt: 8 }}>
        <CircularProgress />
      </Box>
    );
  }

  if (!link) {
    return (
      <Box sx={{ display: 'flex', justifyContent: 'center', mt: 8, px: 2 }}>
        <Card sx={{ maxWidth: 400, width: '100%' }}>
          <CardContent sx={{ textAlign: 'center', py: 4 }}>
            <Typography variant="h6" gutterBottom>
              {error ?? 'Link expired or invalid'}
            </Typography>
            <Typography variant="body2" color="text.secondary">
              {error
                ? 'Check your connection and try again.'
                : 'This invite link has expired or is no longer valid. Ask the league owner to generate a new one.'}
            </Typography>
          </CardContent>
        </Card>
      </Box>
    );
  }

  return (
    <Box sx={{ display: 'flex', justifyContent: 'center', mt: 8, px: 2 }}>
      <Card sx={{ maxWidth: 400, width: '100%' }}>
        <CardContent sx={{ textAlign: 'center', py: 4 }}>
          <Typography variant="overline" color="text.secondary">You&apos;re invited to join</Typography>
          <Typography variant="h5" fontWeight={600} gutterBottom>{link.leagueName}</Typography>
          {error && (
            <Typography variant="body2" color="error" sx={{ mb: 2 }}>{error}</Typography>
          )}
          {user && availableLeagues.some((l) => l.leagueId === link.leagueId) ? (
            <Box sx={{ mt: 2, display: 'flex', flexDirection: 'column', alignItems: 'center', gap: 1 }}>
              <CheckCircleOutlineIcon color="success" sx={{ fontSize: 36 }} />
              <Typography variant="body1" color="text.secondary">
                You&apos;re already a member of this league.
              </Typography>
              <Button variant="outlined" fullWidth onClick={() => navigate('/dashboard')} sx={{ mt: 1 }}>
                Go to Dashboard
              </Button>
            </Box>
          ) : user ? (
            <Button
              variant="contained"
              color="secondary"
              size="large"
              fullWidth
              disabled={joining}
              onClick={() => void handleJoin()}
              sx={{ mt: 2 }}
            >
              {joining ? <CircularProgress size={22} color="inherit" /> : 'Join League'}
            </Button>
          ) : (
            <Button
              variant="contained"
              color="secondary"
              size="large"
              fullWidth
              onClick={handleSignUp}
              sx={{ mt: 2 }}
            >
              Create an account to join
            </Button>
          )}
        </CardContent>
      </Card>
    </Box>
  );
}
