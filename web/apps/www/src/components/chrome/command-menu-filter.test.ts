import { describe, expect, it } from 'vitest';
import type { SearchEntry } from '@/content/search-index';
import { filterSearchEntries, groupBySection } from './command-menu-filter';

const ENTRIES: readonly SearchEntry[] = [
  { title: 'Install Plexor', url: '/docs/getting-started/install', section: 'Getting started' },
  { title: 'Welcome', url: '/docs/getting-started', section: 'Getting started' },
  { title: 'Networking, FIPs, LBs', url: '/docs/concepts/networking', section: 'Concepts' },
  { title: 'Audit log', url: '/docs/concepts/audit', section: 'Concepts' },
];

describe('filterSearchEntries', () => {
  it('returns every entry for an empty or whitespace-only query', () => {
    expect(filterSearchEntries(ENTRIES, '')).toEqual(ENTRIES);
    expect(filterSearchEntries(ENTRIES, '   ')).toEqual(ENTRIES);
  });

  it('matches case-insensitively on title', () => {
    const result = filterSearchEntries(ENTRIES, 'INSTALL');
    expect(result).toEqual([ENTRIES[0]]);
  });

  it('matches on section name too', () => {
    const result = filterSearchEntries(ENTRIES, 'concepts');
    expect(result).toEqual([ENTRIES[2], ENTRIES[3]]);
  });

  it('returns an empty list when nothing matches', () => {
    expect(filterSearchEntries(ENTRIES, 'zzz-no-match')).toEqual([]);
  });
});

describe('groupBySection', () => {
  it('groups entries under their section, preserving first-seen section order', () => {
    const grouped = groupBySection(ENTRIES);
    expect(Array.from(grouped.keys())).toEqual(['Getting started', 'Concepts']);
    expect(grouped.get('Getting started')).toEqual([ENTRIES[0], ENTRIES[1]]);
    expect(grouped.get('Concepts')).toEqual([ENTRIES[2], ENTRIES[3]]);
  });

  it('returns an empty map for an empty input', () => {
    expect(groupBySection([]).size).toBe(0);
  });
});
