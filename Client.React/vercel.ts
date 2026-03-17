import { routes, deploymentEnv } from '@vercel/config/v1';
import type { VercelConfig } from '@vercel/config/v1';

const railwayUrl = deploymentEnv('RAILWAY_URL');

export const config: VercelConfig = {
  rewrites: [
    routes.rewrite('/api/(.*)', `${railwayUrl}/api/$1`),
    routes.rewrite('/(.*)', '/index.html'),
  ],
};
