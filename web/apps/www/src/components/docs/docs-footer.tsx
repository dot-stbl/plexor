import { useRouterState } from '@tanstack/react-router';
import type { ReactNode } from 'react';
import { GITHUB_URL, VERSION_LABEL } from '@/components/chrome/nav-config';
import { FRAME_CLASS } from '@/components/chrome/site-frame';

const DOCS_SOURCE_ROOT = 'web/apps/www/src/routes/(docs)/docs';

/**
 * Resolves the exact `content.mdx` source file for a `/docs/*` pathname.
 * Every doc page's route directory mirrors its URL 1:1 (`/docs/concepts` →
 * `.../docs/concepts/content.mdx`, `/docs/how-to/attach-volume` →
 * `.../docs/how-to/attach-volume/content.mdx` — including chapter-root
 * pages, verified against every route under `src/routes/(docs)/docs/`), so
 * this is a straight path substitution, not a lookup table that can drift.
 */
export function resolveEditUrl(pathname: string, githubUrl: string = GITHUB_URL): string {
  const relative = pathname.startsWith('/docs') ? pathname.slice('/docs'.length) : '';
  return `${githubUrl}/edit/main/${DOCS_SOURCE_ROOT}${relative}/content.mdx`;
}

/**
 * Docs footer — two small bits directly under the article: an "Edit on
 * GitHub" link resolved to the exact source `.mdx` file for the current
 * page (spec §4.7 — was a single constant pointing at the chapter
 * directory root; now per-route), and the shared version chip
 * (`nav-config.ts`, same value the header badge shows).
 */
export function DocsFooter(): ReactNode {
  const pathname = useRouterState({ select: (state) => state.location.pathname });
  const editUrl = resolveEditUrl(pathname);

  return (
    <footer className="border-t border-border">
      <div className={`flex flex-wrap items-center justify-between gap-3 py-6 text-xs text-muted-2 ${FRAME_CLASS}`}>
        <a
          href={editUrl}
          className="transition-colors duration-fast ease-out hover:text-foreground"
        >
          Edit on GitHub →
        </a>
        <span className="font-mono">@plexor/www {VERSION_LABEL}</span>
      </div>
    </footer>
  );
}
