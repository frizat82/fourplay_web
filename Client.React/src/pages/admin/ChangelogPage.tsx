import { Box, Paper, Stack, Typography } from '@mui/material';
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
            {/* A real <ul>/<li> instead of MUI's List/ListItem/ListItemText — ListItem's root is
                flex by default, and ListItemText's own styles (flex, minWidth: 0) only work
                inside that flex layout. Faking a bullet with `display: 'list-item'` broke that
                assumption and left long entries visually overflowing their Paper. Native list
                semantics wrap correctly for free, no CSS override needed. */}
            <Stack component="ul" spacing={0.5} sx={{ pl: 3, m: 0 }}>
              {release.entries.map((entry, i) => (
                // Index, not entry text, as the key — a hand-written changelog can plausibly
                // repeat a bullet's wording within one release; order is stable and entries are
                // never independently reordered, so index is safe here.
                <Typography key={i} component="li" variant="body2">{entry}</Typography>
              ))}
            </Stack>
          </Paper>
        ))}
      </Stack>
    </Box>
  );
}
