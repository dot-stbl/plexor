import { SITE_URL } from '@/components/chrome/nav-config';

/**
 * One entry from a route's `head()` → `{ meta: [...] }` array.
 *
 * Typed loose against the TanStack Router `head()` meta shape so this
 * file stays importable from both the bundler-driven entry-server.tsx
 * (Node/bun, vite SSR build) and the browser — no Node APIs, no DOM,
 * no router internals. The collection helper in `collect-route-head.ts`
 * normalises router-specific shapes into `HeadMetaEntry`.
 */
export interface HeadMetaEntry {
  readonly title?: string;
  readonly name?: string;
  readonly property?: string;
  readonly content?: string;
}

export interface ResolvedMetaTag {
  readonly name?: string;
  readonly property?: string;
  readonly content: string;
}

export interface ResolvedHead {
  readonly title: string;
  readonly description: string | undefined;
  readonly canonical: string;
  readonly meta: readonly ResolvedMetaTag[];
}

const DEFAULT_TITLE = 'plexor';
const DEFAULT_DESCRIPTION = 'plexor — self-hosted cloud platform';

/**
 * Merge matched-route head meta into one resolved head, then derive
 * canonical + Open Graph + Twitter Card tags from the resolved
 * title/description.
 *
 * `entries` must be walked root-first → leaf-last, so the natural
 * last-wins loop below lets the leaf override ancestor defaults
 * without the caller having to dedupe. `pathname` is the router's
 * FINAL resolved location (after any internal `beforeLoad` redirect),
 * so a redirecting route's canonical points at what the page actually
 * renders. See `collect-route-head.ts` for the walk order, and
 * `scripts/prerender/run.ts` for how the final pathname is read.
 */
export function resolveHead(
  entries: readonly HeadMetaEntry[],
  pathname: string,
): ResolvedHead {
  let title = DEFAULT_TITLE;
  let description: string | undefined = DEFAULT_DESCRIPTION;

  for (const entry of entries) {
    if (typeof entry.title === 'string') title = entry.title;
    if (entry.name === 'description' && typeof entry.content === 'string') {
      description = entry.content;
    }
  }

  const canonical = `${SITE_URL}${pathname}`;
  const meta: ResolvedMetaTag[] = [];
  if (description) meta.push({ name: 'description', content: description });
  meta.push({ property: 'og:site_name', content: 'plexor' });
  meta.push({ property: 'og:type', content: 'website' });
  meta.push({ property: 'og:title', content: title });
  if (description) meta.push({ property: 'og:description', content: description });
  meta.push({ property: 'og:url', content: canonical });
  meta.push({ name: 'twitter:card', content: 'summary' });
  meta.push({ name: 'twitter:title', content: title });
  if (description) meta.push({ name: 'twitter:description', content: description });

  return { title, description, canonical, meta };
}
