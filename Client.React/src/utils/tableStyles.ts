import type { SxProps, Theme } from '@mui/material';

/**
 * Pins a table's leading identity column (user/team name) in place while the rest of a wide
 * table scrolls horizontally underneath it — mobile viewports (~390px) can't fit a full data
 * table, so without this the reader loses track of which row they're looking at once they
 * scroll right. Needs an opaque background (not the default transparent TableCell) so sibling
 * cell content doesn't visibly bleed through as it scrolls underneath the pinned column.
 */
export const stickyColumnSx: SxProps<Theme> = {
  position: 'sticky',
  left: 0,
  zIndex: 1,
  backgroundColor: 'background.paper',
};
