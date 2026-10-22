import type { DeployedVersion } from '../types/version';

/**
 * The website's currently deployed build, from the version.json the build writes next to the page
 * (vite.config.ts). Deliberately not the backend's /api/version: the backend deploys separately,
 * so comparing against it showed "new version available" whenever the two differed, and Refresh —
 * which only reloads the website — could never clear it.
 */
export async function getDeployedVersion(): Promise<DeployedVersion> {
  const response = await fetch('/version.json', { cache: 'no-store' });
  if (!response.ok) throw new Error(`version.json: ${response.status}`);
  return (await response.json()) as DeployedVersion;
}
