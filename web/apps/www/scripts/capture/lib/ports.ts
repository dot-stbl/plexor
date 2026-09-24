/**
 * Free-port lookup within the Plexor dev port pool (17100-17199, see
 * `web/docs/PORTS.md`) — capture runs its OWN console mock server on a
 * scratch port so it never collides with a standing `bun run dev` /
 * `bun run shot`. 17101 (www dev) and 17111 (www preview) are
 * explicitly off-limits per the capture-agent brief; the rest of
 * 17150-17159 ("marketplace, future") is unclaimed today, so it's the
 * preferred scratch range, with 17190-17198 ("shared infra") as fallback.
 */

import net from 'node:net';

const CANDIDATES = [17151, 17152, 17153, 17154, 17155, 17156, 17157, 17158, 17159, 17191, 17192, 17193, 17194];

function isPortFree(port: number): Promise<boolean> {
  return new Promise((resolve) => {
    const srv = net.createServer();
    srv.once('error', () => resolve(false));
    srv.once('listening', () => srv.close(() => resolve(true)));
    srv.listen(port, '127.0.0.1');
  });
}

export async function findFreePort(): Promise<number> {
  for (const port of CANDIDATES) {
    if (await isPortFree(port)) return port;
  }
  throw new Error(`No free port among candidates: ${CANDIDATES.join(', ')}. Free one and rerun.`);
}
