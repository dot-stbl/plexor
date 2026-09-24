import { describe, expect, it } from 'vitest';
import { resolveHead } from './head-meta';

describe('resolveHead', () => {
  it('uses default title and description when no entries are provided', () => {
    const result = resolveHead([], '/');

    expect(result.title).toBe('plexor');
    expect(result.description).toBe('plexor — self-hosted cloud platform');
    expect(result.canonical).toBe('https://plexor.stbl.space/');
    expect(result.meta.some((tag) => tag.name === 'description')).toBe(true);
  });

  it('lets the leaf entry win over root defaults for both title and description', () => {
    const result = resolveHead(
      [
        { title: 'Root Layer Title' },
        { name: 'description', content: 'Root layer description.' },
        { title: 'Leaf Title' },
        { name: 'description', content: 'Operator-facing copy.' },
      ],
      '/docs/getting-started/',
    );

    expect(result.title).toBe('Leaf Title');
    expect(result.description).toBe('Operator-facing copy.');
    expect(result.canonical).toBe('https://plexor.stbl.space/docs/getting-started/');
  });

  it('mirrors title and description into OG and Twitter tags', () => {
    const result = resolveHead(
      [
        { title: 'Leaf' },
        { name: 'description', content: 'Leaf desc.' },
      ],
      '/foo/',
    );

    const og = result.meta.filter((tag) => tag.property?.startsWith('og:'));
    const tw = result.meta.filter((tag) => tag.name?.startsWith('twitter:'));

    expect(og.find((tag) => tag.property === 'og:title')?.content).toBe('Leaf');
    expect(og.find((tag) => tag.property === 'og:description')?.content).toBe('Leaf desc.');
    expect(og.find((tag) => tag.property === 'og:url')?.content).toBe('https://plexor.stbl.space/foo/');
    expect(og.find((tag) => tag.property === 'og:type')?.content).toBe('website');
    expect(og.find((tag) => tag.property === 'og:site_name')?.content).toBe('plexor');

    expect(tw.find((tag) => tag.name === 'twitter:card')?.content).toBe('summary');
    expect(tw.find((tag) => tag.name === 'twitter:title')?.content).toBe('Leaf');
    expect(tw.find((tag) => tag.name === 'twitter:description')?.content).toBe('Leaf desc.');
  });

  it('omits description-based tags gracefully when no description is set', () => {
    // Contract: when a leaf route sets only a title, the leaf's
    // title wins and description falls back to the platform
    // default — never `undefined`, never `content: undefined`
    // in the emitted meta tags.
    const result = resolveHead(
      [{ title: 'Title only' }],
      '/leaf/',
    );

    expect(result.title).toBe('Title only');
    expect(result.description).toBe('plexor — self-hosted cloud platform');
    expect(result.meta.some((tag) => tag.name === 'description')).toBe(true);
    expect(result.meta.some((tag) => tag.property === 'og:description')).toBe(true);
    expect(result.meta.some((tag) => tag.name === 'twitter:description')).toBe(true);
    for (const tag of result.meta) {
      expect(tag.content).toBeTruthy();
    }
    expect(result.meta.find((tag) => tag.property === 'og:title')?.content).toBe(
      'Title only',
    );
  });

  it('ignores entries with neither title nor description-bearing content', () => {
    const result = resolveHead(
      [
        { name: 'theme-color', content: '#ffffff' },
        { name: 'robots', content: 'index,follow' },
      ],
      '/anywhere/',
    );

    expect(result.title).toBe('plexor');
    expect(result.meta.some((tag) => tag.name === 'theme-color')).toBe(false);
    expect(result.meta.some((tag) => tag.name === 'robots')).toBe(false);
  });
});
