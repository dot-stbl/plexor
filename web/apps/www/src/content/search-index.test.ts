import { describe, expect, it } from 'vitest';
import { SEARCH_INDEX } from './search-index';

describe('SEARCH_INDEX', () => {
  it('is non-empty', () => {
    expect(SEARCH_INDEX.length).toBeGreaterThan(0);
  });

  it('has one entry per doc page, each with a title, url and section', () => {
    for (const entry of SEARCH_INDEX) {
      expect(entry.title.length).toBeGreaterThan(0);
      expect(entry.url.startsWith('/docs')).toBe(true);
      expect(entry.section.length).toBeGreaterThan(0);
    }
  });

  it('has no duplicate urls', () => {
    const urls = SEARCH_INDEX.map((entry) => entry.url);
    expect(new Set(urls).size).toBe(urls.length);
  });
});
