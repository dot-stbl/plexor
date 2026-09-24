import { describe, expect, it } from 'vitest';
import { CHAPTERS, flattenPages } from './docs-chapters';

describe('flattenPages', () => {
  it('returns one entry per non-"soon" page, in chapter order', () => {
    const expectedCount = CHAPTERS.flatMap((chapter) =>
      chapter.pages.filter((page) => !page.soon),
    ).length;
    expect(flattenPages().length).toBe(expectedCount);
    expect(flattenPages().length).toBeGreaterThan(0);
  });

  it('preserves chapter order and drops "soon" pages', () => {
    const pages = flattenPages();
    expect(pages[0]).toEqual({ slug: '/docs/getting-started', title: 'Welcome' });
    expect(pages.some((page) => page.slug === CHAPTERS[0].slug)).toBe(true);
    expect(pages.every((page) => page.soon !== true)).toBe(true);
  });

  it('has no duplicate slugs', () => {
    const slugs = flattenPages().map((page) => page.slug);
    expect(new Set(slugs).size).toBe(slugs.length);
  });
});
