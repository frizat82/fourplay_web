import { render, screen } from '@testing-library/react';
import { MemoryRouter } from 'react-router-dom';
import RegisterConfirmationPage from '../pages/account/RegisterConfirmationPage';

function renderWithSearch(search: string) {
  return render(
    <MemoryRouter initialEntries={[`/account/registerconfirmation${search}`]}>
      <RegisterConfirmationPage />
    </MemoryRouter>,
  );
}

describe('RegisterConfirmationPage', () => {
  it('shows the check-your-email step as a prominent alert, not easy-to-miss body text', () => {
    // frizat: user testing live reported not noticing this step at all — the message
    // existed but read as plain paragraph text with no visual weight.
    renderWithSearch('?email=user%40test.com');

    const alert = screen.getByRole('alert');
    expect(alert).toHaveTextContent('Check user@test.com to confirm your account.');
  });

  it('still shows an alert with a generic message when no email param is present', () => {
    renderWithSearch('');

    expect(screen.getByRole('alert')).toHaveTextContent('Please check your email to confirm your account.');
  });
});
