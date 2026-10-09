interface Env {
  isProd: boolean;
  nav: { serviceWorker?: { register(url: string): Promise<unknown> } };
  win: { document: { readyState: string }; addEventListener(type: 'load', listener: () => void): void };
}

/**
 * Registers public/sw.js for every user, so the installed app can open from its saved copy on a
 * weak signal instead of a white screen. Previously only the Notifications settings page
 * registered it (for push), so most users never had one. Waits for the page's own load to finish
 * so the worker's downloads don't compete with it, and is skipped in dev, where Vite serves
 * unbundled modules there's nothing to cache.
 */
export function installAppShellWorker(
  { isProd, nav, win }: Env = { isProd: import.meta.env.PROD, nav: navigator, win: window },
): void {
  if (!isProd || !nav.serviceWorker) return;
  const sw = nav.serviceWorker;
  // A failure just means no offline copy — the app works the same without it.
  const register = () => void sw.register('/sw.js').catch(() => {});
  if (win.document.readyState === 'complete') register();
  else win.addEventListener('load', register);
}
