import { render, screen } from '@testing-library/react';
import { vi } from 'vitest';
import VersionFooter from '../components/VersionFooter';

describe('VersionFooter', () => {
  afterEach(() => vi.unstubAllEnvs());

  // frizat: this used to link out to the GitHub commit — dropped so the repo (and its history)
  // isn't reachable from a public page. Keep the SHA as plain text only, for deploy-freshness.
  it('renders a truncated 7-char SHA as plain text, with no outbound link', () => {
    vi.stubEnv('VITE_APP_VERSION', 'abcdef1234567890');

    render(<VersionFooter />);

    expect(screen.getByText('abcdef1')).toBeInTheDocument();
    expect(screen.queryByRole('link')).not.toBeInTheDocument();
  });

  it('renders nothing when VITE_APP_VERSION is unset (local dev)', () => {
    vi.stubEnv('VITE_APP_VERSION', '');

    const { container } = render(<VersionFooter />);

    expect(container).toBeEmptyDOMElement();
  });
});
