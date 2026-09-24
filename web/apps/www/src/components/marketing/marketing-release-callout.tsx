import type { ComponentType } from 'react';
import { Link } from '@tanstack/react-router';
import { StatusPill } from '@/components/ui/status-pill';
import { groupByShipped, sortByVersionDescending } from '@/components/changelog/sort-entries';
import type { ChangelogFrontmatter } from '@/components/changelog/types';
import { CHAPTERS } from '@/components/docs/docs-chapters';

interface ChangelogModule {
  readonly default: ComponentType;
  readonly frontmatter: ChangelogFrontmatter;
}

/**
 * Latest release callout (spec §3.8, replaces the inline roadmap) —
 * data-driven, same `import.meta.glob` pattern the changelog list page
 * uses (`components/changelog/changelog-list.tsx`), per the §7.2
 * cross-agent interface note. `remark-mdx-frontmatter` is wired in
 * `vite.config.ts` (landed after this file's first draft), so each MDX
 * module's YAML block is available as `mod.frontmatter` without parsing
 * rendered output.
 *
 * Takes the newest entry with `status: "shipped"` (not just the highest
 * version number — v0.3+ are "next"/"design" and shouldn't headline a
 * "latest release" callout) and renders its title + first 2-3 bullets.
 * Renders nothing if the glob is empty (no shipped entries yet) rather
 * than crashing or showing a broken card.
 */
const MODULES = import.meta.glob<ChangelogModule>('/src/content/changelog/*.mdx', {
  eager: true,
});

const ENTRIES: readonly ChangelogFrontmatter[] = sortByVersionDescending(
  Object.values(MODULES).map((mod) => mod.frontmatter),
);

const { shipped } = groupByShipped(ENTRIES);
const latestShipped: ChangelogFrontmatter | undefined = shipped[0];

export function MarketingReleaseCallout() {
  if (!latestShipped) return null;
  const bullets = latestShipped.bullets.slice(0, 3);
  const docsLinks = CHAPTERS.slice(0, 5);

  return (
    <div className="grid gap-8 md:grid-cols-2 md:items-start">
      <div>
        <div className="flex items-center gap-2">
          <p className="text-sm font-medium text-muted-foreground">Latest release</p>
          <StatusPill variant="ok" hideDot size="sm">
            {latestShipped.version}
          </StatusPill>
        </div>
        <h3 className="mt-2 text-xl font-semibold tracking-tight text-foreground">{latestShipped.title}</h3>
        <ul className="mt-3 space-y-1.5">
          {bullets.map((bullet) => (
            <li key={bullet} className="flex items-start gap-2 text-sm leading-6 text-muted-foreground">
              <span className="mt-0.5 text-ok">✓</span>
              {bullet}
            </li>
          ))}
        </ul>
        <Link
          to="/changelog"
          className="mt-4 inline-block text-sm font-medium text-foreground underline underline-offset-4 hover:no-underline"
        >
          View the full changelog →
        </Link>
      </div>
      <div>
        <p className="text-sm font-medium text-muted-foreground">Explore the docs</p>
        <ul className="mt-3 flex flex-col gap-2">
          {docsLinks.map((chapter) => (
            <li key={chapter.slug}>
              <Link
                to={chapter.slug}
                className="flex items-center justify-between text-sm text-foreground hover:text-muted-foreground"
              >
                <span>{chapter.label}</span>
                <span aria-hidden="true">→</span>
              </Link>
            </li>
          ))}
        </ul>
      </div>
    </div>
  );
}
