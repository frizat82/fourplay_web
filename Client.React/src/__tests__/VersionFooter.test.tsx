import { render, screen } from '@testing-library/react';
import { vi } from 'vitest';
import VersionFooter from '../components/VersionFooter';

describe('VersionFooter', () => {
  afterEach(() => vi.unstubAllEnvs());

  it('renders a truncated 7-char SHA linking to the GitHub commit', () => {
    vi.stubEnv('VITE_APP_VERSION', 'abcdef1234567890');

    render(<VersionFooter />);

    const link = screen.getByRole('link', { name: 'abcdef1' });
    expect(link).toHaveAttribute('href', 'https://github.com/frizat82/fourplay_web/commit/abcdef1234567890');
  });

  it('renders nothing when VITE_APP_VERSION is unset (local dev)', () => {
    vi.stubEnv('VITE_APP_VERSION', '');

    const { container } = render(<VersionFooter />);

    expect(container).toBeEmptyDOMElement();
  });
});
