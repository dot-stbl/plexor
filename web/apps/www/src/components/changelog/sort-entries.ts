import type { ChangelogStatus } from './types';

/**
 * Pure version-compare and grouping helpers for the changelog — no
 * React, no MDX, no glob loading, so they're trivially unit-testable
 * (see `sort-entries.test.ts`). Versions are "vMAJOR.MINOR" strings
 * (`v0.1`..`v0.6` today); comparison is numeric per dot-separated
 * segment, not lexicographic — a plain string sort would misorder
 * `v0.10` before `v0.9` the day the project ships a tenth minor.
 */
export function parseVersion(version: string): readonly number[] {
  const stripped = version.trim().replace(/^v/i, '');
  return stripped.split('.').map((part) => Number.parseInt(part, 10) || 0);
}

/** Descending comparator: negative when `a` is newer than `b`. */
export function compareVersionsDescending(a: string, b: string): number {
  const partsA = parseVersion(a);
  const partsB = parseVersion(b);
  const length = Math.max(partsA.length, partsB.length);
  for (let index = 0; index < length; index += 1) {
    const diff = (partsB[index] ?? 0) - (partsA[index] ?? 0);
    if (diff !== 0) return diff;
  }
  return 0;
}

/** Sorts by version, newest first. Returns a new array — never mutates the input. */
export function sortByVersionDescending<T extends { readonly version: string }>(
  entries: readonly T[],
): T[] {
  return [...entries].sort((a, b) => compareVersionsDescending(a.version, b.version));
}

export interface ChangelogGroups<T> {
  readonly shipped: readonly T[];
  readonly roadmap: readonly T[];
}

/**
 * Splits entries into shipped releases vs. the roadmap (`next`/`design`
 * status). Relative order within each group is preserved — callers sort
 * first with `sortByVersionDescending`, then group.
 */
export function groupByShipped<T extends { readonly status: ChangelogStatus }>(
  entries: readonly T[],
): ChangelogGroups<T> {
  return {
    shipped: entries.filter((entry) => entry.status === 'shipped'),
    roadmap: entries.filter((entry) => entry.status !== 'shipped'),
  };
}
