import { describe, expect, it } from 'vitest';
import { resolveEditUrl } from './docs-footer';

const GITHUB_URL = 'https://github.com/dot-stbl/plexor';

describe('resolveEditUrl', () => {
  it('resolves a chapter-root page to its content.mdx', () => {
    expect(resolveEditUrl('/docs/concepts', GITHUB_URL)).toBe(
      `${GITHUB_URL}/edit/main/web/apps/www/src/routes/(docs)/docs/concepts/content.mdx`,
    );
  });

  it('resolves a nested sub-page to its content.mdx', () => {
    expect(resolveEditUrl('/docs/how-to/attach-volume', GITHUB_URL)).toBe(
      `${GITHUB_URL}/edit/main/web/apps/www/src/routes/(docs)/docs/how-to/attach-volume/content.mdx`,
    );
  });

  it('falls back to the docs root for a non-/docs pathname', () => {
    expect(resolveEditUrl('/changelog', GITHUB_URL)).toBe(
      `${GITHUB_URL}/edit/main/web/apps/www/src/routes/(docs)/docs/content.mdx`,
    );
  });
});
