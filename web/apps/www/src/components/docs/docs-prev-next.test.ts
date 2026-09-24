import { describe, expect, it } from 'vitest';
import type { DocsPage } from './docs-chapters';
import { getPagerNeighbors } from './docs-prev-next';

const PAGES: readonly DocsPage[] = [
  { slug: '/docs/a', title: 'A' },
  { slug: '/docs/b', title: 'B' },
  { slug: '/docs/c', title: 'C' },
];

describe('getPagerNeighbors', () => {
  it('returns only next for the first page', () => {
    expect(getPagerNeighbors(PAGES, '/docs/a')).toEqual({ prev: null, next: PAGES[1] });
  });

  it('returns prev and next for a middle page', () => {
    expect(getPagerNeighbors(PAGES, '/docs/b')).toEqual({ prev: PAGES[0], next: PAGES[2] });
  });

  it('returns only prev for the last page', () => {
    expect(getPagerNeighbors(PAGES, '/docs/c')).toEqual({ prev: PAGES[1], next: null });
  });

  it('returns neither when the pathname is not in the list', () => {
    expect(getPagerNeighbors(PAGES, '/docs/zzz')).toEqual({ prev: null, next: null });
  });

  it('returns neither for an empty list', () => {
    expect(getPagerNeighbors([], '/docs/a')).toEqual({ prev: null, next: null });
  });
});
