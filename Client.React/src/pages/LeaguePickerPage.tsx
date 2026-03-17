import { Card, CardContent, List, ListItemButton, ListItemText, Typography } from '@mui/material';
import PageHeader from '../components/PageHeader';
import { useSession } from '../services/session';

export default function LeaguePickerPage() {
  const { availableLeagues, currentLeague, selectLeague } = useSession();

  return (
    <div>
      <PageHeader title="Pick a League" />
      <Card>
        <CardContent>
          {availableLeagues.length === 0 ? (
            <Typography variant="body1">No leagues assigned. Contact an administrator.</Typography>
          ) : (
            <List>
              {availableLeagues.map((league) => (
                <ListItemButton
                  key={league.leagueId}
                  selected={league.leagueId === currentLeague}
                  onClick={() => selectLeague(league.leagueId)}
                >
                  <ListItemText
                    primary={league.leagueName ?? 'Unnamed League'}
                  />
                </ListItemButton>
              ))}
            </List>
          )}
        </CardContent>
      </Card>
    </div>
  );
}
