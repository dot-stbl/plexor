import type { ComponentType } from 'react';

export type ChangelogStatus = 'shipped' | 'next' | 'design';

/**
 * Frontmatter contract every `src/content/changelog/*.mdx` file exports.
 * `vite.config.ts` runs `remark-mdx-frontmatter` after `remark-frontmatter`,
 * which turns each file's YAML block into `export const frontmatter = {...}`
 * — so `import.meta.glob('/src/content/changelog/*.mdx', { eager: true })`
 * resolves to `Record<string, { default: ComponentType; frontmatter: ChangelogFrontmatter }>`.
 *
 * `bullets` is the single authored copy of the release's curated bullet
 * list (relocated verbatim from the old `marketing-roadmap.tsx` `PHASES`
 * array). Each MDX file's body renders `frontmatter.bullets` directly
 * (remark-mdx-frontmatter binds `frontmatter` in scope for the rest of
 * the file, not just as an export) — so there is exactly one place the
 * bullet text is authored, and any importer (this list, the landing
 * page's release callout) can read the same strings without parsing
 * rendered MDX output.
 */
export interface ChangelogFrontmatter {
  readonly version: string;
  readonly title: string;
  readonly status: ChangelogStatus;
  readonly bullets: readonly string[];
}

/** One resolved changelog release: frontmatter plus its compiled MDX body. */
export interface ChangelogEntryData extends ChangelogFrontmatter {
  readonly Content: ComponentType;
}
