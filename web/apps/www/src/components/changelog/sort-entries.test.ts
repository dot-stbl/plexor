import { describe, expect, it } from 'vitest';
import {
  compareVersionsDescending,
  groupByShipped,
  parseVersion,
  sortByVersionDescending,
} from './sort-entries';

describe('parseVersion', () => {
  it('parses "vMAJOR.MINOR" into numeric segments', () => {
    expect(parseVersion('v0.2')).toEqual([0, 2]);
    expect(parseVersion('v1.10')).toEqual([1, 10]);
  });

  it('is case-insensitive on the leading v', () => {
    expect(parseVersion('V0.3')).toEqual([0, 3]);
  });
});

describe('compareVersionsDescending', () => {
  it('orders a newer version above an older one', () => {
    expect(compareVersionsDescending('v0.2', 'v0.1')).toBeLessThan(0);
    expect(compareVersionsDescending('v0.1', 'v0.2')).toBeGreaterThan(0);
  });

  it('compares numerically, not lexicographically', () => {
    expect(compareVersionsDescending('v0.10', 'v0.9')).toBeLessThan(0);
  });

  it('treats equal versions as equal', () => {
    expect(compareVersionsDescending('v0.2', 'v0.2')).toBe(0);
  });
});

describe('sortByVersionDescending', () => {
  const entries = [
    { version: 'v0.1' },
    { version: 'v0.6' },
    { version: 'v0.3' },
    { version: 'v0.2' },
    { version: 'v0.5' },
    { version: 'v0.4' },
  ];

  it('sorts newest first', () => {
    const sorted = sortByVersionDescending(entries);
    expect(sorted.map((entry) => entry.version)).toEqual([
      'v0.6',
      'v0.5',
      'v0.4',
      'v0.3',
      'v0.2',
      'v0.1',
    ]);
  });

  it('does not mutate the input array', () => {
    sortByVersionDescending(entries);
    expect(entries[0]?.version).toBe('v0.1');
  });
});

describe('groupByShipped', () => {
  const entries = [
    { version: 'v0.2', status: 'shipped' as const },
    { version: 'v0.1', status: 'shipped' as const },
    { version: 'v0.3', status: 'next' as const },
    { version: 'v0.5', status: 'design' as const },
  ];

  it('splits shipped entries from next/design entries, preserving order', () => {
    const { shipped, roadmap } = groupByShipped(entries);
    expect(shipped.map((entry) => entry.version)).toEqual(['v0.2', 'v0.1']);
    expect(roadmap.map((entry) => entry.version)).toEqual(['v0.3', 'v0.5']);
  });

  it('returns empty groups for an empty input', () => {
    const { shipped, roadmap } = groupByShipped([]);
    expect(shipped).toEqual([]);
    expect(roadmap).toEqual([]);
  });
});
