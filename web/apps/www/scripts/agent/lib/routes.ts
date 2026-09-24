/**
 * Route discovery — the list of URL paths derived from the file-based
 * routes under `src/routes/**` (TanStack Router file-route convention).
 * Used by `bun run shot routes` and `bun run shot page` ($param check).
 *
 * Adapted from `web/apps/console/scripts/agent/lib/routes.ts`: console
 * has no pathless route-group directories, so its `deriveRoutePath`
 * never had to strip them. `www` does — `(marketing)` and `(docs)` are
 * TanStack Router "pathless group" directories: the parens mean the
 * segment is collapsed from the URL entirely. Without stripping them,
 * every marketing page would incorrectly list as `/(marketing)/...` and
 * `(marketing)/route.tsx` / `(docs)/route.tsx` (the two outer layout
 * files, which own no URL of their own) would falsely register as a
 * route for `/`. `SAMPLES` is empty — `www` has no `$param` routes today.
 */

import { readdirSync, statSync } from 'node:fs';
import { join, relative, sep } from 'node:path';
import { ROOT } from './servers';

export const ROUTES_DIR = join(ROOT, 'src', 'routes');

export interface RouteEntry {
  /** URL path, e.g. '/docs/getting-started'. */
  readonly routePath: string;
  /** Source file relative to the www app root, e.g. 'src/routes/(docs)/docs/index.tsx'. */
  readonly file: string;
  /** true if the path contains a `$param` segment. */
  readonly hasParam: boolean;
  /** Known concrete URL for a `$param` route, if any (see SAMPLES). */
  readonly sample?: string;
}

/** `www` has no dynamic `$param` routes today — kept for parity with console's shape. */
export const SAMPLES: Readonly<Record<string, string>> = {};

function walk(dir: string, acc: string[]): string[] {
  for (const name of readdirSync(dir)) {
    const full = join(dir, name);
    const st = statSync(full);
    if (st.isDirectory()) {
      walk(full, acc);
    } else if (/\.tsx?$/.test(name)) {
      acc.push(full);
    }
  }
  return acc;
}

/** File name for screenshots/reports: '/docs/getting-started' → 'docs_getting-started'; '/' → 'root'. */
export function slugForPath(routePath: string): string {
  if (routePath === '/') return 'root';
  return routePath.replace(/^\//, '').replace(/\//g, '_');
}

/**
 * '(marketing)/index.tsx' → { routePath: '/', isLayout: false }
 * '(docs)/docs/getting-started/index.tsx' → '/docs/getting-started'
 * '(docs)/route.tsx' → { routePath: '/', isLayout: true } (see isGroupRootLayout below —
 *   this exact shape is filtered out of the listing before this even matters)
 *
 * Pathless group segments (`(marketing)`, `(docs)`) are stripped before
 * deriving the path — TanStack Router collapses them from the URL.
 */
function deriveRoutePath(relFile: string): { routePath: string; isLayout: boolean } {
  const noExt = relFile.replace(/\.tsx?$/, '');
  const segments = noExt.split('/').filter((s) => !/^\(.+\)$/.test(s));
  const last = segments[segments.length - 1];
  const isLayout = last === 'route';
  if (isLayout || last === 'index') {
    segments.pop();
  }
  const routePath = segments.length === 0 ? '/' : `/${segments.join('/')}`;
  return { routePath, isLayout };
}

/**
 * True for a `route.tsx` that sits directly (and only) inside one
 * pathless group folder, e.g. `(marketing)/route.tsx` or
 * `(docs)/route.tsx` — these are the outer layout wrappers for an
 * entire route group and own no distinct URL of their own (every real
 * page under the group already gets its own entry). Without this guard
 * both would otherwise derive to `/` and either shadow or be shadowed by
 * the group's real index route.
 */
function isGroupRootLayout(relFile: string): boolean {
  const noExt = relFile.replace(/\.tsx?$/, '');
  const segments = noExt.split('/');
  return segments.length === 2 && /^\(.+\)$/.test(segments[0]!) && segments[1] === 'route';
}

/** The app's route list, one per URL path (a route.tsx layout never shadows a leaf file at the same path). */
export function listRoutes(): RouteEntry[] {
  const files = walk(ROUTES_DIR, []).sort();
  const byPath = new Map<string, RouteEntry>();

  for (const abs of files) {
    const rel = relative(ROUTES_DIR, abs).split(sep).join('/');
    if (/\.(test|stories)\.tsx?$/.test(rel)) continue;
    if (rel === '__root.tsx') continue;
    if (isGroupRootLayout(rel)) continue;

    const { routePath, isLayout } = deriveRoutePath(rel);
    if (isLayout && byPath.has(routePath)) continue; // leaf wins over layout

    byPath.set(routePath, {
      routePath,
      file: `src/routes/${rel}`,
      hasParam: routePath.includes('$'),
      sample: SAMPLES[routePath],
    });
  }

  return Array.from(byPath.values()).sort((a, b) => a.routePath.localeCompare(b.routePath));
}
