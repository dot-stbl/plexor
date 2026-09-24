import type { SearchEntry } from '@/content/search-index';

/**
 * Pure filter/group helpers for `CommandMenu` — kept in their own file
 * (no React, no DOM) so they're trivially unit-testable (see
 * `command-menu-filter.test.ts`).
 */

/** Case-insensitive substring match on title or section. Empty query returns everything. */
export function filterSearchEntries(
  entries: readonly SearchEntry[],
  query: string,
): readonly SearchEntry[] {
  const needle = query.trim().toLowerCase();
  if (needle.length === 0) return entries;
  return entries.filter(
    (entry) =>
      entry.title.toLowerCase().includes(needle) || entry.section.toLowerCase().includes(needle),
  );
}

/** Groups entries by `section`, preserving first-seen section order. */
export function groupBySection(
  entries: readonly SearchEntry[],
): ReadonlyMap<string, readonly SearchEntry[]> {
  const map = new Map<string, SearchEntry[]>();
  for (const entry of entries) {
    const bucket = map.get(entry.section);
    if (bucket) {
      bucket.push(entry);
    } else {
      map.set(entry.section, [entry]);
    }
  }
  return map;
}
