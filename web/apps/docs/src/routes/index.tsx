import { createFileRoute, Link } from '@tanstack/react-router';
import { PlexorMark } from '@plexor/ui/brand';
import { presets } from '@plexor/ui/themes';

/**
 * Docs landing page — single hero block + the chapter grid. Each
 * chapter card is a Link to the route's `index.mdx`. v1 ships two
 * chapters (getting started, concepts); the rest are stubs waiting on
 * content authoring.
 *
 * The chapter list is local to this file on purpose: there are only
 * three entries, they don't need a registry, and inlining them lets
 * the build statically enumerate every chapter without reading any
 * route files at module evaluation time.
 */

interface Chapter {
  readonly to: '/concepts';
  readonly eyebrow: string;
  readonly title: string;
  readonly summary: string;
}

const CHAPTERS: readonly Chapter[] = [
  {
    to: '/concepts',
    eyebrow: 'Chapter 01',
    title: 'Concepts',
    summary:
      'The mental model: organisation, team, folder. The resource scope hierarchy, the principle behind it, and what it means in practice.',
  },
] as const;

export const Route = createFileRoute('/')({
  component: HomePage,
  head: () => ({
    meta: [
      { title: 'plexor docs' },
      {
        name: 'description',
        content: 'Self-hosted cloud platform — operator docs.',
      },
    ],
  }),
});

export function HomePage() {
  const totalPresets = presets.length;
  return (
    <div className="space-y-12">
      <section className="space-y-4">
        <div className="flex items-center gap-3">
          <PlexorMark className="h-10 w-10 text-foreground" />
          <span className="font-mono text-xs uppercase tracking-[0.16em] text-muted-2">
            plexor
          </span>
        </div>
        <h1 className="text-4xl font-semibold tracking-tight text-foreground">
          Operator documentation
        </h1>
        <p className="max-w-2xl text-base leading-7 text-muted-foreground">
          Plexor is a self-hosted cloud platform. This site walks operators
          through the mental model, the day-to-day workflows, and the
          failure modes of running it on bare metal or in a single-tenant
          cluster. The chrome is monochrome by design — accent stays out
          of the way so the diagrams carry the meaning.
        </p>
        <p className="font-mono text-xs text-muted-2">
          {totalPresets} theme presets available · light / dark / noir
        </p>
      </section>

      <section className="space-y-4">
        <h2 className="text-xs font-medium uppercase tracking-[0.16em] text-muted-2">
          Chapters
        </h2>
        <ul className="grid gap-3 sm:grid-cols-1">
          {CHAPTERS.map((chapter) => (
            <li key={chapter.to}>
              <Link
                to={chapter.to}
                className="block rounded-lg border border-border bg-card p-5 transition-colors duration-fast ease-out hover:border-foreground/30"
              >
                <div className="mb-1 font-mono text-[10px] uppercase tracking-[0.16em] text-muted-2">
                  {chapter.eyebrow}
                </div>
                <div className="mb-2 text-base font-semibold text-foreground">
                  {chapter.title}
                </div>
                <p className="text-sm leading-6 text-muted-foreground">
                  {chapter.summary}
                </p>
              </Link>
            </li>
          ))}
        </ul>
      </section>
    </div>
  );
}