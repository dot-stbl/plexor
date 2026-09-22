import type { ReactNode } from 'react';

/**
 * Docs footer — two small bits under the article: a build-time version
 * chip + an "Edit on GitHub" pointer that links to the source path.
 *
 * The "Edit on GitHub" link uses the article's expected source path
 * (`src/routes/(docs)/docs/<chapter>/`). For the v1 placeholder it
 * points at the docs directory broadly; a future build step can
 * resolve it per-route from the file path.
 */
const EDIT_URL =
  'https://github.com/dot-stbl/plexor/edit/main/web/apps/docs/src/routes/(docs)/docs';

export function DocsFooter(): ReactNode {
  return (
    <footer className="border-t border-border">
      <div className="mx-auto flex max-w-7xl flex-wrap items-center justify-between gap-3 px-6 py-6 text-xs text-muted-2">
        <a
          href={EDIT_URL}
          className="transition-colors duration-fast ease-out hover:text-foreground"
        >
          Edit on GitHub →
        </a>
        <span className="font-mono">@plexor/docs v0.2 pre-stable</span>
      </div>
    </footer>
  );
}