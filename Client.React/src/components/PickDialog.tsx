import {
  Avatar,
  Box,
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

interface PickDialogProps {
  open: boolean;
  onClose: () => void;
  userNames: string[];
  userNamesOver: string[];
  userNamesUnder: string[];
  teamAbbr: string;
  logo: string;
}

export default function PickDialog({
  open,
  onClose,
  userNames,
  userNamesOver,
  userNamesUnder,
  teamAbbr,
  logo,
}: PickDialogProps) {
  return (
    <Dialog open={open} onClose={onClose} maxWidth="sm" fullWidth>
      <DialogContent>
        <Stack direction="row" alignItems="center" justifyContent="space-between" sx={{ mb: 2 }}>
          {(userNames.length > 0 || userNamesOver.length > 0 || userNamesUnder.length > 0) && (
            <>
              <Typography variant="h5" fontWeight={700}>
                Team: {teamAbbr}
              </Typography>
              <img src={logo} alt={teamAbbr} width={60} style={{ borderRadius: 8 }} />
            </>
          )}
          {userNamesOver.length > 0 && <ArrowCircleUpIcon fontSize="large" />}
          {userNamesUnder.length > 0 && <ArrowCircleDownIcon fontSize="large" />}
        </Stack>
        <Divider sx={{ mb: 2 }} />

        {userNames.length > 0 && (
          <Box sx={{ mb: 3 }}>
            <Typography variant="h6" sx={{ mb: 1 }}>
              Spread
            </Typography>
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
          </Box>
        )}

        {userNamesOver.length > 0 && (
          <Box sx={{ mb: 3 }}>
            <Typography variant="h6" sx={{ mb: 1 }}>
              Over
            </Typography>
            <List dense>
              {userNamesOver.map((user) => (
                <ListItem key={user}>
                  <ListItemAvatar>
                    <Avatar color="primary">{user[0]}</Avatar>
                  </ListItemAvatar>
                  <ListItemText primary={user} />
                </ListItem>
              ))}
            </List>
          </Box>
        )}

        {userNamesUnder.length > 0 && (
          <Box>
            <Typography variant="h6" sx={{ mb: 1 }}>
              Under
            </Typography>
            <List dense>
              {userNamesUnder.map((user) => (
                <ListItem key={user}>
                  <ListItemAvatar>
                    <Avatar color="secondary">{user[0]}</Avatar>
                  </ListItemAvatar>
                  <ListItemText primary={user} />
                </ListItem>
              ))}
            </List>
          </Box>
        )}
      </DialogContent>
    </Dialog>
  );
}
