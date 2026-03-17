import { Paper, Typography } from '@mui/material';

export default function NoLeague() {
  return (
    <Paper sx={{ p: 4, textAlign: 'center' }}>
      <Typography variant="subtitle1" color="secondary">
        Please select a league (top right)
      </Typography>
    </Paper>
  );
}
