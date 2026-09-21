/**
 * Minimal static-file server for the Storybook test-runner.
 *
 * Serves `dist-storybook/` (built by `bun run build:storybook`) on a
 * configurable port and exits when killed. Used by `bun run test:visual`
 * and `bun run test:visual:update` to host the static Storybook that
 * the test-runner browses against.
 *
 * Why this exists:
 *  - `bunx serve` had resolution issues on this stack; Python's
 *    `http.server` is fine on CI ubuntu but drags cross-platform noise.
 *  - Storybook itself bundles a preview server (`bun run storybook`),
 *    but it watches + rebuilds — wrong shape for deterministic tests.
 *  - We need a tiny, single-run, exit-on-kill HTTP server with a known
 *    Content-Type for `.html` (no extension guessing from URL).
 *
 * Run: `bun run scripts/serve-static.ts <dir> [port]`. Default port
 * matches the test-runner's default URL (http://127.0.0.1:6006).
 */

import { existsSync } from 'node:fs';
import { join, normalize, resolve } from 'node:path';

const dir = resolve(process.argv[2] ?? 'dist-storybook');
const port = Number.parseInt(process.argv[3] ?? '6006', 10);

if (!existsSync(dir)) {
  console.error(`[serve-static] ${dir} does not exist — run \`bun run build:storybook\` first.`);
  process.exit(1);
}

const MIME = {
  '.html': 'text/html; charset=utf-8',
  '.js': 'application/javascript; charset=utf-8',
  '.mjs': 'application/javascript; charset=utf-8',
  '.css': 'text/css; charset=utf-8',
  '.json': 'application/json; charset=utf-8',
  '.svg': 'image/svg+xml',
  '.png': 'image/png',
  '.jpg': 'image/jpeg',
  '.jpeg': 'image/jpeg',
  '.webp': 'image/webp',
  '.ico': 'image/x-icon',
  '.woff': 'font/woff',
  '.woff2': 'font/woff2',
  '.ttf': 'font/ttf',
  '.txt': 'text/plain; charset=utf-8',
  '.map': 'application/json; charset=utf-8',
} as const;

const server = Bun.serve({
  port,
  hostname: '127.0.0.1',
  development: false,
  async fetch(req) {
    const url = new URL(req.url);
    let pathname = decodeURIComponent(url.pathname);

    // Path-traversal guard: refuse anything resolving outside `dir`.
    const candidate = normalize(join(dir, pathname));
    if (!candidate.startsWith(dir)) {
      return new Response('forbidden', { status: 403 });
    }

    // SPA fallback: serve index.html for routes without an extension.
    if (!existsSync(candidate)) {
      const lastSegment = pathname.split('/').pop() ?? '';
      if (!lastSegment.includes('.')) {
        pathname = '/index.html';
      } else {
        return new Response('not found', { status: 404 });
      }
    }

    const filePath = pathname === '/' ? join(dir, 'index.html') : normalize(join(dir, pathname));
    const ext = filePath.slice(filePath.lastIndexOf('.')).toLowerCase() as keyof typeof MIME;
    const contentType = MIME[ext] ?? 'application/octet-stream';

    const file = Bun.file(filePath);
    if (!(await file.exists())) {
      return new Response('not found', { status: 404 });
    }

    return new Response(file, { headers: { 'Content-Type': contentType } });
  },
});

console.log(`[serve-static] ${dir} → http://${server.hostname}:${server.port}`);
