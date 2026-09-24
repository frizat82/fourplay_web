import { Component, type ErrorInfo, type ReactNode } from 'react';
import { Box, Button, Typography } from '@mui/material';

interface Props {
  children: ReactNode;
  onReload?: () => void;
}

interface State {
  error: Error | null;
}

/**
 * Catches a route that fails to render — most often a lazy route chunk that couldn't download
 * (flaky phone signal, or a deploy mid-session that chunkReloadGuard couldn't recover). Without
 * it React unmounts the whole app, nav included, leaving a blank screen with no way forward.
 * Callers key it by pathname, so navigating to another route remounts it and clears the error.
 */
export default class RouteErrorBoundary extends Component<Props, State> {
  state: State = { error: null };

  static getDerivedStateFromError(error: Error): Partial<State> {
    return { error };
  }

  componentDidCatch(error: Error, info: ErrorInfo) {
    console.error('Route failed to render', error, info.componentStack);
  }

  render() {
    if (!this.state.error) return this.props.children;
    const reload = this.props.onReload ?? (() => window.location.reload());
    return (
      <Box sx={{ textAlign: 'center', py: 6, px: 2 }}>
        <Typography variant="h6" gutterBottom>Couldn't load this page</Typography>
        <Typography variant="body2" color="text.secondary" sx={{ mb: 2 }}>
          Check your connection and try again.
        </Typography>
        <Button variant="contained" onClick={reload}>Reload</Button>
      </Box>
    );
  }
}
