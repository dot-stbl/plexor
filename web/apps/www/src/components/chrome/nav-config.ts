/**
 * Chrome-wide constants — single source of truth for `SiteHeader` /
 * `SiteFooter` / `CommandMenu`. Every value below is a verified fact, not
 * a guess (see the Foundation agent's report for how each was checked):
 *
 *   - `GITHUB_URL`      — `git remote -v` in the repo root (origin, both
 *                          fetch and push): https://github.com/dot-stbl/plexor
 *   - `CLONE_COMMAND` /
 *     `RUN_COMMAND`     — the real host project path,
 *                          `src/host/Plexor.Host/Plexor.Host.csproj`
 *                          (`plexor.slnx`). There is no packaged install
 *                          command yet — this is the actual clone+run path
 *                          from the root README's stack section.
 *   - `VERSION` /
 *     `VERSION_LABEL`   — the existing version chip already shipped in
 *                          `docs-footer.tsx` / `marketing-footer.tsx`
 *                          (`v0.2 pre-stable`), relocated here so header,
 *                          footer and command menu all read one value.
 */

export const GITHUB_URL = 'https://github.com/dot-stbl/plexor';

export const CLONE_COMMAND = `git clone ${GITHUB_URL}`;

export const RUN_COMMAND = 'dotnet run --project src/host/Plexor.Host';

export const VERSION = 'v0.2';

export const VERSION_LABEL = `${VERSION} · pre-stable`;

export interface NavItem {
  readonly label: string;
  readonly to: string;
}

/** Marketing-variant nav items (§2.2) — docs variant shows a breadcrumb instead. */
export const MARKETING_NAV_ITEMS: readonly NavItem[] = [
  { label: 'Docs', to: '/docs/getting-started' },
  { label: 'Changelog', to: '/changelog' },
];
