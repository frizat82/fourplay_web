import React, { useMemo } from 'react';
import ReactDOM from 'react-dom/client';
import { BrowserRouter } from 'react-router-dom';
import { CssBaseline, ThemeProvider } from '@mui/material';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { Analytics } from '@vercel/analytics/react';
import { SpeedInsights } from '@vercel/speed-insights/react';
import App from './App';
import { createAppTheme } from './app/theme';
import { AuthProvider } from './services/auth';
import { SessionProvider } from './services/session';
import { SportsProvider } from './services/sport';
import { ToastProvider } from './services/toast';
import { ThemeModeProvider, useThemeMode } from './services/theme';
import { useVersionCheck } from './utils/useVersionCheck';
import UpdateBanner from './components/UpdateBanner';
import RouteErrorBoundary from './components/RouteErrorBoundary';
import { installChunkReloadGuard } from './utils/chunkReloadGuard';
import { installAppShellWorker } from './utils/appShellWorker';
// Bundled rather than loaded from Google Fonts: a render-blocking stylesheet on another host
// could stall the whole launch on a weak signal (the iOS white-screen report). Only the weights
// the UI uses — Space Grotesk for body/headings, Rajdhani for the brand.
import '@fontsource/space-grotesk/400.css';
import '@fontsource/space-grotesk/500.css';
import '@fontsource/space-grotesk/600.css';
import '@fontsource/space-grotesk/700.css';
import '@fontsource/rajdhani/600.css';
import '@fontsource/rajdhani/700.css';
import './app/global.css';

installChunkReloadGuard();
installAppShellWorker();

const queryClient = new QueryClient({
  defaultOptions: {
    queries: {
      refetchOnWindowFocus: false,
      retry: 1,
    },
  },
});

function ThemedApp() {
  const { mode } = useThemeMode();
  const theme = useMemo(() => createAppTheme(mode), [mode]);
  const { mismatch } = useVersionCheck();
  return (
    <ThemeProvider theme={theme}>
      <CssBaseline />
      <UpdateBanner mismatch={mismatch} />
      {/* Outermost catch: a throw in a provider (auth, session, sport) would otherwise unmount the
          whole app and leave an empty page. Inside ThemeProvider so the fallback is readable in
          dark mode. Route-level boundaries in App/AppLayout keep the nav up for page failures. */}
      <RouteErrorBoundary>
        <BrowserRouter>
          <ToastProvider>
            <SportsProvider>
              <AuthProvider>
                <SessionProvider>
                  <App />
                </SessionProvider>
              </AuthProvider>
            </SportsProvider>
          </ToastProvider>
        </BrowserRouter>
      </RouteErrorBoundary>
    </ThemeProvider>
  );
}

ReactDOM.createRoot(document.getElementById('root')!).render(
  <React.StrictMode>
    <QueryClientProvider client={queryClient}>
      <ThemeModeProvider>
        <ThemedApp />
      </ThemeModeProvider>
    </QueryClientProvider>
    <Analytics />
    <SpeedInsights />
  </React.StrictMode>
);
