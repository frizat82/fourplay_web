import {
  Avatar,
  Dialog,
  DialogContent,
  DialogTitle,
  Divider,
  IconButton,
  List,
  ListItem,
  ListItemAvatar,
  ListItemText,
  Stack,
  Typography,
} from '@mui/material';
import ArrowCircleUpIcon from '@mui/icons-material/ArrowCircleUp';
import ArrowCircleDownIcon from '@mui/icons-material/ArrowCircleDown';
import CloseIcon from '@mui/icons-material/Close';
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
    <Dialog
      open={open}
      onClose={onClose}
      maxWidth="sm"
      fullWidth
      PaperProps={{ sx: { maxHeight: '70vh' } }}
    >
      <DialogTitle sx={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', pb: 1 }}>
        <Stack direction="row" alignItems="center" gap={1}>
          {pickType === 'Spread' && (
            <>
              <TeamHelmet abbr={teamAbbr} size={44} />
              <Typography variant="h5" fontWeight={700}>{teamAbbr}</Typography>
            </>
          )}
          {pickType === 'Over' && (
            <>
              <ArrowCircleUpIcon fontSize="large" color="success" />
              <Typography variant="h5" fontWeight={700}>Over</Typography>
            </>
          )}
          {pickType === 'Under' && (
            <>
              <ArrowCircleDownIcon fontSize="large" color="error" />
              <Typography variant="h5" fontWeight={700}>Under</Typography>
            </>
          )}
        </Stack>
        <IconButton onClick={onClose} aria-label="Close" sx={{ minWidth: 44, minHeight: 44 }}>
          <CloseIcon />
        </IconButton>
      </DialogTitle>
      <Divider />
      <DialogContent sx={{ overflowY: 'auto' }}>
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
