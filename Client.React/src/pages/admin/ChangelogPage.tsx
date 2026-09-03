import { Box, List, ListItem, ListItemText, Paper, Stack, Typography } from '@mui/material';
import PageHeader from '../../components/PageHeader';
import changelogRaw from '../../../CHANGELOG.md?raw';
import { parseChangelog } from '../../utils/parseChangelog';

// Bundled at build time (Vite ?raw import) — always exactly what shipped in this deploy, no
// backend endpoint or extra request needed. See Client.React/CHANGELOG.md for the source.
// Deliberately inside Client.React/ (not the repo root) — that's Vercel's configured build root
// (vercel.json lives here), and a file outside it isn't guaranteed to be available at build time.
const releases = parseChangelog(changelogRaw);

export default function AdminChangelogPage() {
  return (
    <Box>
      <PageHeader title="Changelog" />

      {releases.length === 0 && (
        <Typography color="text.secondary" sx={{ textAlign: 'center', mt: 4 }}>
          No entries yet.
        </Typography>
      )}

      <Stack spacing={3}>
        {releases.map((release) => (
          <Paper key={release.heading} sx={{ p: 2 }}>
            <Typography variant="h6" component="h6" gutterBottom>
              {release.heading}
            </Typography>
            <List dense disablePadding>
              {release.entries.map((entry, i) => (
                // Index, not entry text, as the key — a hand-written changelog can plausibly
                // repeat a bullet's wording within one release; order is stable and entries are
                // never independently reordered, so index is safe here.
                <ListItem key={i} disablePadding sx={{ display: 'list-item', listStyleType: 'disc', ml: 3 }}>
                  <ListItemText primary={entry} />
                </ListItem>
              ))}
            </List>
          </Paper>
        ))}
      </Stack>
    </Box>
  );
}
