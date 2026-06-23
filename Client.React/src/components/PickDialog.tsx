import {
  Avatar,
  Dialog,
  DialogContent,
  Divider,
  List,
  ListItem,
  ListItemAvatar,
  ListItemText,
  Stack,
  Typography,
} from '@mui/material';
import ArrowCircleUpIcon from '@mui/icons-material/ArrowCircleUp';
import ArrowCircleDownIcon from '@mui/icons-material/ArrowCircleDown';
import TeamHelmet from './sports/TeamHelmet';

interface PickDialogProps {
  open: boolean;
  onClose: () => void;
  userNames: string[];
  userNamesOver: string[];
  userNamesUnder: string[];
  teamAbbr: string;
  pickType: 'Spread' | 'Over' | 'Under';
}

export default function PickDialog({
  open,
  onClose,
  userNames,
  userNamesOver,
  userNamesUnder,
  teamAbbr,
  pickType,
}: PickDialogProps) {
  return (
    <Dialog open={open} onClose={onClose} maxWidth="sm" fullWidth>
      <DialogContent>
        <Stack direction="row" alignItems="center" justifyContent="space-between" sx={{ mb: 2 }}>
          {pickType === 'Spread' && (
            <>
              <Typography variant="h5" fontWeight={700}>{teamAbbr}</Typography>
              <TeamHelmet abbr={teamAbbr} size={60} />
            </>
          )}
          {pickType === 'Over' && (
            <Stack direction="row" alignItems="center" gap={1}>
              <ArrowCircleUpIcon fontSize="large" color="success" />
              <Typography variant="h5" fontWeight={700}>Over</Typography>
            </Stack>
          )}
          {pickType === 'Under' && (
            <Stack direction="row" alignItems="center" gap={1}>
              <ArrowCircleDownIcon fontSize="large" color="error" />
              <Typography variant="h5" fontWeight={700}>Under</Typography>
            </Stack>
          )}
        </Stack>
        <Divider sx={{ mb: 2 }} />

        {userNames.length > 0 && (
          <List dense>
            {userNames.map((user) => (
              <ListItem key={user}>
                <ListItemAvatar>
                  <Avatar>{user[0]}</Avatar>
                </ListItemAvatar>
                <ListItemText primary={user} />
              </ListItem>
            ))}
          </List>
        )}

        {userNamesOver.length > 0 && (
          <List dense>
            {userNamesOver.map((user) => (
              <ListItem key={user}>
                <ListItemAvatar>
                  <Avatar>{user[0]}</Avatar>
                </ListItemAvatar>
                <ListItemText primary={user} />
              </ListItem>
            ))}
          </List>
        )}

        {userNamesUnder.length > 0 && (
          <List dense>
            {userNamesUnder.map((user) => (
              <ListItem key={user}>
                <ListItemAvatar>
                  <Avatar>{user[0]}</Avatar>
                </ListItemAvatar>
                <ListItemText primary={user} />
              </ListItem>
            ))}
          </List>
        )}
      </DialogContent>
    </Dialog>
  );
}
