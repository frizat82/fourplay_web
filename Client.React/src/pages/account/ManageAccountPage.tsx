import { Button, Card, CardContent, Divider, Grid, Stack, Typography } from '@mui/material';
import { useNavigate } from 'react-router-dom';
import { useAuth } from '../../services/auth';
import { useSession } from '../../services/session';

export default function ManageAccountPage() {
  const { user } = useAuth();
  const { availableLeagues } = useSession();
  const navigate = useNavigate();

  return (
    <Card>
      <CardContent>
        <Typography variant="h5" gutterBottom>
          Welcome, {user?.name}
        </Typography>
        <Divider sx={{ mb: 2 }} />
        <Typography variant="subtitle1" gutterBottom>
          Leagues
        </Typography>
        <Grid container spacing={2}>
          {availableLeagues.length > 0 ? (
            availableLeagues.map((league) => (
              <Grid size={{ xs: 12, sm: 6, md: 4 }} key={league.leagueId}>
                <Card>
                  <CardContent>
                    <Typography variant="h6">{league.leagueName ?? 'Unknown'}</Typography>
                    <Typography variant="body2">
                      Wins: <b>0</b> | Losses: <b>0</b>
                    </Typography>
                  </CardContent>
                </Card>
              </Grid>
            ))
          ) : (
            <Typography>No leagues found.</Typography>
          )}
        </Grid>
        <Divider sx={{ my: 3 }} />
        <Stack direction="row" spacing={2}>
          <Button variant="contained" onClick={() => navigate('/account/manage/changepassword')}>
            Change Password
          </Button>
          <Button variant="outlined" onClick={() => navigate('/account/manage')}>
            Manage Account
          </Button>
        </Stack>
      </CardContent>
    </Card>
  );
}
