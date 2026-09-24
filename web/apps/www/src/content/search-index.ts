import { CHAPTERS } from '@/components/docs/docs-sidebar';

/**
 * Global search index (§4.3) — one entry per doc page, derived directly
 * from `CHAPTERS` in `docs-sidebar.tsx` (the sidebar's own source of
 * truth) rather than hand-copied, so the two can't drift out of sync.
 * Pages flagged `soon` (not live yet) are excluded — nothing in the
 * command menu should link to a page that 404s.
 */
export interface SearchEntry {
  readonly title: string;
  readonly url: string;
  readonly section: string;
}

export const SEARCH_INDEX: readonly SearchEntry[] = CHAPTERS.flatMap((chapter) =>
  chapter.pages
    .filter((page) => !page.soon)
    .map((page) => ({ title: page.title, url: page.slug, section: chapter.label })),
);
