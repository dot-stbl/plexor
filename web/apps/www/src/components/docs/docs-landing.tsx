import { Link } from '@tanstack/react-router';
import { Search } from '@nine-thirty-five/material-symbols-react/rounded/700';
import { Button } from '@/components/ui/button';
import { EYEBROW_CLASS, Panel } from '@/components/chrome/panel';
import { useCommandMenu } from '@/components/chrome/command-menu-store';
import { CHAPTERS } from './docs-chapters';

/**
 * `/docs` entry page (YC-informed restyle, 2026-09-24) — replaces the
 * old immediate redirect to `/docs/getting-started` with a real
 * landing: a hero panel (search trigger reusing the global
 * `CommandMenu`) + a chapter-cards grid. Renders inside the shared
 * `(docs)` layout (`routes/(docs)/route.tsx`), so the sidebar and TOC
 * are still present — the TOC renders an empty aside when there are no
 * `.docs-prose` headings on the page, which is the case here (this page
 * is chrome, not prose, so it deliberately doesn't get the
 * `.docs-prose` class).
 *
 * Uses `Panel` directly (not `PanelContainer`) — the docs 3-column grid
 * already constrains this page's width via `FRAME_CLASS`, so a second
 * contained-width wrapper would double up the gutter.
 */
const CHAPTER_BLURBS: Readonly<Record<string, string>> = {
  'Getting started': 'Get a brand-new operator to a working Plexor in under 30 minutes.',
  Concepts:
    'The mental models — orgs, teams, folders, workloads, networking, storage, quotas, audit.',
  'How-to': 'Task-oriented recipes: create a workload, attach a volume, add a user, and more.',
  Admin: 'Operations and hardening — backups, upgrades, access control, audit hygiene.',
  Reference: 'Exhaustive catalogs: the REST API surface, permissions, quotas.',
  'FAQ / Troubleshooting':
    'Symptom → cause → fix for the first place to look when something is on fire.',
};

export function DocsLanding() {
  const { setOpen } = useCommandMenu();

  return (
    <div className="flex flex-col gap-4 md:gap-6">
      <Panel fill="card">
        <p className={EYEBROW_CLASS}>Docs</p>
        <h1 className="mt-2 text-3xl font-extrabold tracking-tight text-foreground md:text-4xl">
          Documentation
        </h1>
        <p className="mt-3 max-w-xl text-base leading-7 text-muted-foreground">
          Install, run and operate Plexor — from first boot to hardening a
          production cluster.
        </p>
        <Button
          variant="outline"
          onClick={() => setOpen(true)}
          className="mt-6 w-full max-w-md justify-between text-muted-2"
        >
          <span className="flex items-center gap-1.5">
            <Search className="size-3.5" />
            Search docs…
          </span>
          <kbd className="rounded border border-border px-1 font-mono text-[10px] text-muted-2">
            Ctrl K
          </kbd>
        </Button>
      </Panel>

      <Panel fill="muted">
        <p className={EYEBROW_CLASS}>Chapters</p>
        <h2 className="mt-2 text-2xl font-extrabold tracking-tight text-foreground">Start here.</h2>
        <div className="mt-6 grid grid-cols-1 gap-4 sm:grid-cols-2 md:gap-6 lg:grid-cols-3">
          {CHAPTERS.map((chapter) => {
            const blurb = CHAPTER_BLURBS[chapter.label];
            if (!blurb) return null;
            return (
              <Link
                key={chapter.slug}
                to={chapter.slug}
                className="block rounded-2xl bg-card p-6 transition-colors duration-fast ease-out hover:bg-card/80"
              >
                <span className="font-mono text-xs text-muted-2">
                  {String(chapter.index).padStart(2, '0')}
                </span>
                <h3 className="mt-2 text-base font-semibold text-foreground">{chapter.label}</h3>
                <p className="mt-1.5 text-sm leading-6 text-muted-foreground">{blurb}</p>
              </Link>
            );
          })}
        </div>
      </Panel>
    </div>
  );
}
