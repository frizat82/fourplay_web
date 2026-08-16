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

export default function JoinLeaguePage() {
  const { token } = useParams<{ token: string }>();
  const navigate = useNavigate();
  const { user } = useAuth();
  const { reloadLeagues } = useSession();

  const [loading, setLoading] = useState(true);
  const [joining, setJoining] = useState(false);
  const [link, setLink] = useState<LeagueInviteLinkDto | null>(null);
  const [invalid, setInvalid] = useState(false);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    if (!token) { setInvalid(true); setLoading(false); return; }
    validateInviteLink(token).then((result) => {
      if (!result) setInvalid(true);
      else setLink(result);
    }).finally(() => setLoading(false));
  }, [token]);

  const handleSignUp = () => {
    navigate(`/account/register?inviteLinkToken=${token}&returnUrl=/join/${token}`);
  };

  const handleJoin = async () => {
    if (!token) return;
    setJoining(true);
    setError(null);
    try {
      await joinViaLink(token);
      await reloadLeagues();
      navigate('/dashboard');
    } catch {
      setError('Failed to join league. The link may have expired.');
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

  if (invalid) {
    return (
      <Box sx={{ display: 'flex', justifyContent: 'center', mt: 8, px: 2 }}>
        <Card sx={{ maxWidth: 400, width: '100%' }}>
          <CardContent sx={{ textAlign: 'center', py: 4 }}>
            <Typography variant="h6" gutterBottom>Link expired or invalid</Typography>
            <Typography variant="body2" color="text.secondary">
              This invite link has expired or is no longer valid. Ask the league owner to generate a new one.
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
          <Typography variant="h5" fontWeight={600} gutterBottom>{link?.leagueName}</Typography>
          {error && (
            <Typography variant="body2" color="error" sx={{ mb: 2 }}>{error}</Typography>
          )}
          {user ? (
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
