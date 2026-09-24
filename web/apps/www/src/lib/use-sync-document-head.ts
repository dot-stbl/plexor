import { useEffect } from 'react';
import { useMatches, useRouter } from '@tanstack/react-router';
import { collectRouteHead } from './collect-route-head';
import { resolveHead } from './head-meta';

function upsertMeta(name: string, content: string): void {
  let tag = document.head.querySelector<HTMLMetaElement>(`meta[name="${name}"]`);
  if (!tag) {
    tag = document.createElement('meta');
    tag.setAttribute('name', name);
    document.head.appendChild(tag);
  }
  tag.setAttribute('content', content);
}

function upsertPropertyMeta(property: string, content: string): void {
  let tag = document.head.querySelector<HTMLMetaElement>(`meta[property="${property}"]`);
  if (!tag) {
    tag = document.createElement('meta');
    tag.setAttribute('property', property);
    document.head.appendChild(tag);
  }
  tag.setAttribute('content', content);
}

function upsertCanonical(href: string): void {
  let tag = document.head.querySelector<HTMLLinkElement>('link[rel="canonical"]');
  if (!tag) {
    tag = document.createElement('link');
    tag.setAttribute('rel', 'canonical');
    document.head.appendChild(tag);
  }
  tag.setAttribute('href', href);
}

/**
 * Sync the document's `<title>` + a small, fixed allow-list of head
 * tags to the matched route's `head()` after every client navigation.
 *
 * TanStack Router 1.91 (pinned — see `web/apps/www/package.json`) does
 * NOT ship `HeadContent`/`Meta`/`Scripts`: the `head()` route option
 * is accepted but never invoked by the router. On the server, the
 * prerender script in `scripts/prerender/run.ts` collects and emits
 * these tags statically. On the client, this hook is the only thing
 * keeping a user clicking between `/docs/*` pages from getting stuck
 * on the first page's prerendered tags.
 *
 * Allow-list: `meta[name="description"]`, `meta[property^="og:"]`
 * (title/description/url), `meta[name^="twitter:"]`,
 * `link[rel="canonical"]`. Tags this hook does not own
 * (`theme-color`, `robots`, `application-name`, `generator`,
 * `color-scheme`, the favicon) are left untouched — they live in
 * `index.html` and stay valid across every route.
 */
export function useSyncDocumentHead(): void {
  const router = useRouter();
  const matches = useMatches();

  useEffect(() => {
    if (typeof document === 'undefined') return;

    const head = resolveHead(
      collectRouteHead(
        router as unknown as Parameters<typeof collectRouteHead>[0],
        matches,
      ),
      window.location.pathname,
    );

    document.title = head.title;
    if (head.description) upsertMeta('description', head.description);

    upsertPropertyMeta('og:site_name', 'plexor');
    upsertPropertyMeta('og:type', 'website');
    upsertPropertyMeta('og:title', head.title);
    if (head.description) upsertPropertyMeta('og:description', head.description);
    upsertPropertyMeta('og:url', window.location.href);

    upsertMeta('twitter:card', 'summary');
    upsertMeta('twitter:title', head.title);
    if (head.description) upsertMeta('twitter:description', head.description);

    upsertCanonical(window.location.href);
  }, [router, matches]);
}
