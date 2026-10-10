import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { ThemeProvider } from '@mui/material/styles';
import { createAppTheme } from '../app/theme';
import RetryPanel from '../components/RetryPanel';

// /style-guide: MUI's disabled state flattens a filled button to gray. While a retry is in
// flight the button must stay recognisably the same primary button, in both modes.
describe('RetryPanel', () => {
  for (const mode of ['light', 'dark'] as const) {
    it(`keeps the primary fill while a retry is in flight (${mode})`, async () => {
      const theme = createAppTheme(mode);
      render(
        <ThemeProvider theme={theme}>
          <RetryPanel title="Can't reach IV League" message="Check your signal." actionLabel="Retry" onAction={() => new Promise(() => {})} />
        </ThemeProvider>,
      );
      const button = screen.getByRole('button', { name: 'Retry' });
      await userEvent.click(button);

      expect(button).toBeDisabled();
      const hex = theme.palette.primary.main;
      const rgb = `rgb(${[1, 3, 5].map(i => parseInt(hex.slice(i, i + 2), 16)).join(', ')})`;
      expect(getComputedStyle(button).backgroundColor).toBe(rgb);
    });
  }

  it('re-enables the button once the action settles', async () => {
    let finish!: () => void;
    render(<RetryPanel title="t" message="m" actionLabel="Retry" onAction={() => new Promise<void>(r => { finish = r; })} />);
    const button = screen.getByRole('button', { name: 'Retry' });
    await userEvent.click(button);
    expect(button).toBeDisabled();
    finish();
    await screen.findByRole('button', { name: 'Retry' });
    await expect.poll(() => (button as HTMLButtonElement).disabled).toBe(false);
  });
});
