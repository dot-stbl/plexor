#!/usr/bin/env bun
/**
 * prerender — turns `dist/` from a Vite SPA into a real static
 * site: one `index.html` per real route, plus `sitemap.xml` /
 * `robots.txt` and a pure-CSR `404.html` shell.
 *
 * Runs AFTER `vite build` (chained in `package.json`'s `build`
 * script). Re-uses the same `vite.config.ts` plugins
 * (TanStackRouterVite, MDX, react, tailwind) via Vite's programmatic
 * SSR build — the bundle in `dist-server/entry-server.js` is what
 * Node/bun can't run on its own (MDX + the router-plugin codegen).
 *
 * File-by-file contract:
 *   dist/index.html                  ← left intact; rewritten with
 *                                       resolved title/head, real #root
 *   dist/404.html                    ← cloned from dist/index.html FIRST,
 *                                       before any mutation; its
 *                                       `#root` stays empty so main.tsx
 *                                       picks the createRoot path
 *   dist/<route>/index.html          ← one per leaf route
 *   dist/sitemap.xml                 ← one <loc> per DISTINCT final
 *                                       pathname (dedupes the
 *                                       hypothetical /docs/ redirect
 *                                       case automatically)
 *   dist/robots.txt                  ← Allow: / + Sitemap pointer
 *   dist-server/                     ← intermediate; deleted on success
 *
 * Pages whose rendered `appHtml` is suspiciously small or missing
 * expected site chrome (a header landmark in the marketing layout)
 * fail the whole script with exit 1 — never silently ship a blank
 * page.
 */

import { mkdirSync, readFileSync, rmSync, writeFileSync } from 'node:fs';
import { dirname, join } from 'node:path';

import { build } from 'vite';
import { SITE_URL } from '../../src/components/chrome/nav-config';

const PROJECT_ROOT = join(import.meta.dir, '..', '..');
const DIST = join(PROJECT_ROOT, 'dist');
const DIST_SERVER = join(PROJECT_ROOT, 'dist-server');
const ENTRY_SSR = join(PROJECT_ROOT, 'src', 'entry-server.tsx');
const SSR_BUNDLE = join(DIST_SERVER, 'entry-server.js');

const MIN_HTML_LENGTH = 200;
const MARKER = '<header';

interface ResolvedMetaTag {
  readonly name?: string;
  readonly property?: string;
  readonly content: string;
}

interface ResolvedHead {
  readonly title: string;
  readonly description?: string;
  readonly canonical: string;
  readonly meta: readonly ResolvedMetaTag[];
}

interface RenderResult {
  readonly appHtml: string;
  readonly finalPathname: string;
  readonly head: ResolvedHead;
}

interface EntryServerModule {
  getAllRoutePaths: () => string[];
  renderRoute: (path: string) => Promise<RenderResult>;
}

function escapeHtml(value: string): string {
  return value
    .replace(/&/g, '&amp;')
    .replace(/</g, '&lt;')
    .replace(/>/g, '&gt;')
    .replace(/"/g, '&quot;');
}

function renderMetaTag(tag: ResolvedMetaTag): string {
  if (tag.name !== undefined) {
    return `<meta name="${escapeHtml(tag.name)}" content="${escapeHtml(tag.content)}">`;
  }
  if (tag.property !== undefined) {
    return `<meta property="${escapeHtml(tag.property)}" content="${escapeHtml(tag.content)}">`;
  }
  return '';
}

function injectHead(template: string, head: ResolvedHead): string {
  let html = template.replace(/<title>[^<]*<\/title>/, `<title>${escapeHtml(head.title)}</title>`);

  // Strip the static placeholder description (the only one in the
  // current index.html) so the resolved head's tags aren't duplicated.
  html = html.replace(/\s*<meta\s+name="description"\s+content="[^"]*"\s*\/?>/, '');

  // Defensive: clear any pre-existing canonical tag (there shouldn't
  // be one in index.html, but Vite plugins sometimes add things).
  html = html.replace(/\s*<link\s+rel="canonical"[^>]*>/, '');

  const metaTags = head.meta
    .map(renderMetaTag)
    .filter((s) => s.length > 0)
    .join('');
  const canonicalTag = `<link rel="canonical" href="${escapeHtml(head.canonical)}">`;
  const insert = `${canonicalTag}${metaTags}`;

  return html.replace('</head>', `${insert}</head>`);
}

function injectBody(html: string, appHtml: string): string {
  return html.replace('<div id="root"></div>', `<div id="root">${appHtml}</div>`);
}

function pathToFile(routePath: string): string {
  if (routePath === '/') return join(DIST, 'index.html');
  const segments = routePath.split('/').filter((s) => s.length > 0);
  return join(DIST, ...segments, 'index.html');
}

async function buildSsrBundle(): Promise<void> {
  console.log('[ssr] vite build ssr → dist-server/');
  await build({
    build: {
      ssr: ENTRY_SSR,
      outDir: DIST_SERVER,
      emptyOutDir: true,
      sourcemap: false,
      rollupOptions: { output: { format: 'es' } },
    },
  });
}

async function main(): Promise<void> {
  const t0 = Date.now();

  // 1. Read the just-built index.html (vite resolved the %env-style
  //    placeholders and stamped asset URLs).
  const templateHtml = readFileSync(join(DIST, 'index.html'), 'utf-8');

  // 2. Clone that template to 404.html BEFORE any per-route mutation,
  //    so its `#root` stays empty (main.tsx's `hasChildNodes()`
  //    branch picks the createRoot path for it).
  writeFileSync(join(DIST, '404.html'), templateHtml);
  console.log('[404] wrote dist/404.html (CSR-only fallback, empty #root)');

  // 3. Build the SSR bundle via Vite (it loads vite.config.ts from
  //    the cwd automatically, so MDX + router-codegen plugins run).
  await buildSsrBundle();

  // 4. Dynamically import the just-emitted SSR bundle. Symbol-level
  //    import keeps the SSR-only compile out of the prerender's own
  //    bundle (we don't need to type-check it twice).
  const serverMod = (await import(SSR_BUNDLE)) as EntryServerModule;

  // 5. Discover every prerenderable leaf path.
  const paths = serverMod.getAllRoutePaths();
  console.log(`[ssr] discovered ${paths.length} prerenderable routes`);

  // 6. Render each path. Track distinct final pathnames for the
  //    sitemap — the brief's redirect-following case (where one
  //    requested URL resolves to another URL's content) auto-dedupes.
  const finalToRequested = new Map<string, string[]>();
  let rendered = 0;

  for (const requestedPath of paths) {
    let result: RenderResult;
    try {
      result = await serverMod.renderRoute(requestedPath);
    } catch (err) {
      throw new Error(
        `prerender failed for "${requestedPath}": ${
          err instanceof Error ? err.message : String(err)
        }`,
      );
    }

    if (
      result.appHtml.length < MIN_HTML_LENGTH ||
      !result.appHtml.includes(MARKER)
    ) {
      throw new Error(
        `prerender sanity check failed for "${requestedPath}": appHtml looks blank or truncated.\n` +
          `  appHtml length: ${result.appHtml.length} chars (need >= ${MIN_HTML_LENGTH})\n` +
          `  contains "${MARKER}": ${result.appHtml.includes(MARKER)}\n` +
          `  first 200 chars: ${result.appHtml.slice(0, 200)}`,
      );
    }

    let html = injectHead(templateHtml, result.head);
    html = injectBody(html, result.appHtml);

    const outPath = pathToFile(requestedPath);
    mkdirSync(dirname(outPath), { recursive: true });
    writeFileSync(outPath, html);
    rendered++;

    const requested = finalToRequested.get(result.finalPathname) ?? [];
    requested.push(requestedPath);
    finalToRequested.set(result.finalPathname, requested);
  }

  // 7. sitemap.xml — distinct final pathnames only.
  const distinctUrls = Array.from(finalToRequested.keys()).sort();
  const sitemap =
    '<?xml version="1.0" encoding="UTF-8"?>\n' +
    '<urlset xmlns="http://www.sitemaps.org/schemas/sitemap/0.9">\n' +
    distinctUrls
      .map((finalPath) => `  <url><loc>${escapeHtml(`${SITE_URL}${finalPath}`)}</loc></url>`)
      .join('\n') +
    '\n</urlset>\n';
  writeFileSync(join(DIST, 'sitemap.xml'), sitemap);
  console.log(`[sitemap] wrote ${distinctUrls.length} entries → dist/sitemap.xml`);

  // 8. robots.txt — Allow the entire site + point at the sitemap.
  const robots =
    'User-agent: *\n' +
    'Allow: /\n' +
    `Sitemap: ${SITE_URL}/sitemap.xml\n`;
  writeFileSync(join(DIST, 'robots.txt'), robots);
  console.log('[robots] wrote dist/robots.txt');

  // 9. Clean up the SSR bundle directory so it never ships inside
  //    the final dist/. `force: true` in case a partial earlier
  //    build left files behind.
  rmSync(DIST_SERVER, { recursive: true, force: true });
  console.log('[cleanup] removed dist-server/');

  const elapsed = Date.now() - t0;
  console.log(
    `\n[done] ${rendered} pages prerendered (${distinctUrls.length} distinct after redirects) in ${elapsed}ms`,
  );
}

main().catch((err) => {
  console.error(err);
  process.exit(1);
});
