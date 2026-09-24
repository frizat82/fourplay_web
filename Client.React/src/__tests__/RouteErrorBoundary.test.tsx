import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { vi } from 'vitest';
import RouteErrorBoundary from '../components/RouteErrorBoundary';

function Boom(): never {
  throw new Error('Failed to fetch dynamically imported module');
}

// Lazy route chunks can fail to download (flaky phone signal, a deploy mid-session). Without a
// boundary React 19 unmounts the whole tree — nav included — leaving a blank screen.
describe('RouteErrorBoundary', () => {
  beforeEach(() => vi.spyOn(console, 'error').mockImplementation(() => {}));

  it('renders its children when nothing throws', () => {
    render(<RouteErrorBoundary><p>page content</p></RouteErrorBoundary>);
    expect(screen.getByText('page content')).toBeInTheDocument();
  });

  it('shows a retry message instead of a blank screen when a child throws', () => {
    render(<RouteErrorBoundary><Boom /></RouteErrorBoundary>);
    expect(screen.getByText(/couldn't load this page/i)).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /reload/i })).toBeInTheDocument();
  });

  it('reloads the page when Reload is tapped', async () => {
    const reload = vi.fn();
    render(<RouteErrorBoundary onReload={reload}><Boom /></RouteErrorBoundary>);
    await userEvent.click(screen.getByRole('button', { name: /reload/i }));
    expect(reload).toHaveBeenCalledTimes(1);
  });

  it('recovers when keyed to a new route (navigating away)', () => {
    const { rerender } = render(<RouteErrorBoundary key="/leaderboard"><Boom /></RouteErrorBoundary>);
    expect(screen.getByText(/couldn't load this page/i)).toBeInTheDocument();

    rerender(<RouteErrorBoundary key="/picks"><p>picks page</p></RouteErrorBoundary>);
    expect(screen.getByText('picks page')).toBeInTheDocument();
  });
});
