import type { ComponentType } from 'react';
import { EYEBROW_CLASS, Panel } from '@/components/chrome/panel';
import { ChangelogEntry } from './changelog-entry';
import { groupByShipped, sortByVersionDescending } from './sort-entries';
import type { ChangelogEntryData, ChangelogFrontmatter } from './types';

interface ChangelogModule {
  readonly default: ComponentType;
  readonly frontmatter: ChangelogFrontmatter;
}

/**
 * Eager glob — same pattern the landing implementer's
 * `marketing-release-callout.tsx` reads independently for the latest
 * entry (§5 / §7.2 interface note). Eager (not lazy) because there are
 * only a handful of releases and the page needs every one of them to
 * sort/group before first paint.
 */
const MODULES = import.meta.glob<ChangelogModule>('/src/content/changelog/*.mdx', {
  eager: true,
});

const ENTRIES: readonly ChangelogEntryData[] = sortByVersionDescending(
  Object.values(MODULES).map((mod) => ({ ...mod.frontmatter, Content: mod.default })),
);

const { shipped, roadmap } = groupByShipped(ENTRIES);

/**
 * The changelog's two groups (spec §5, amendment A8): shipped releases,
 * then a clearly separate "Roadmap" group for `next`/`design` entries.
 * `id="planned"` matches the anchor `SiteFooter`'s "Roadmap" link
 * already points at (`/changelog#planned`) — not the `#roadmap` id a
 * literal reading of the brief would suggest; verified against
 * `site-footer.tsx` before choosing this id.
 */
export function ChangelogList() {
  return (
    <>
      <Panel fill="muted">
        <p className={EYEBROW_CLASS}>Releases</p>
        <h2 className="mt-2 mb-2 text-3xl font-extrabold tracking-tight text-foreground">Shipped</h2>
        <div>
          {shipped.map((entry, index) => (
            <ChangelogEntry key={entry.version} entry={entry} isFirst={index === 0} />
          ))}
        </div>
      </Panel>

      <Panel id="planned" fill="sunken">
        <p className={EYEBROW_CLASS}>Roadmap</p>
        <h2 className="mt-2 mb-2 text-3xl font-extrabold tracking-tight text-foreground">
          What&rsquo;s next
        </h2>
        <p className="mb-6 max-w-2xl text-sm leading-6 text-muted-foreground">
          Open proposals, not promises — versions move between here and Shipped as they land.
        </p>
        <div>
          {roadmap.map((entry, index) => (
            <ChangelogEntry key={entry.version} entry={entry} isFirst={index === 0} />
          ))}
        </div>
      </Panel>
    </>
  );
}
