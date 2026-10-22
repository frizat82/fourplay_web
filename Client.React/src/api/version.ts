import type { DeployedVersion } from '../types/version';

const TIMEOUT_MS = 15_000;

/**
 * The website's currently deployed build, from the version.json the build writes next to the page
 * (vite.config.ts). Deliberately not the backend's /api/version: the backend deploys separately,
 * so comparing against it showed "new version available" whenever the two differed, and Refresh —
 * which only reloads the website — could never clear it.
 *
 * Anything but a real SHA throws (no banner) rather than reading as "a different version", which
 * would latch the banner on for good.
 */
export async function getDeployedVersion(): Promise<DeployedVersion> {
  const response = await fetch('/version.json', { cache: 'no-store', signal: AbortSignal.timeout(TIMEOUT_MS) });
  if (!response.ok) throw new Error(`version.json: ${response.status}`);
  const body: unknown = await response.json();
  const sha = (body as Partial<DeployedVersion> | null)?.sha;
  if (typeof sha !== 'string' || sha === '') throw new Error('version.json: no sha');
  return { sha };
}
