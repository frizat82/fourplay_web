/**
 * MUI's `disabled` state flattens contained (filled) buttons to uniform gray regardless of
 * `color`. Keep the real color at reduced opacity instead of falling back to generic gray — see
 * /style-guide's "MUI button conventions".
 */
export const lockedFillSx = (color: 'primary' | 'success' | 'info') => ({
  '&.Mui-disabled': {
    color: `${color}.contrastText`,
    backgroundColor: `${color}.main`,
    opacity: 0.6,
  },
});
