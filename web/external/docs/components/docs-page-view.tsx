import { DocsPage, DocsBody, DocsDescription, DocsTitle } from 'fumadocs-ui/layouts/docs/page';
import { createRelativeLink } from 'fumadocs-ui/mdx';
import type { Metadata } from 'next';
import { getMDXComponents } from '@/components/mdx';
import { source } from '@/lib/source';

type DocsPage = (typeof source)['$inferPage'];

/**
 * Shared docs-page renderer used by both locale route trees
 * (`app/(ru)` and `app/(en)/en`). `page.data.body` is the compiled MDX.
 * Deliberately NOT a client component: `page` carries the compiled MDX body
 * (a function), which cannot cross the server→client boundary — rendering
 * stays in the prerendered server tree, exactly like the canonical fumadocs
 * page pattern.
 */
export function DocsPageView({ page }: { page: DocsPage }) {
  const MDX = page.data.body;

  return (
    <DocsPage toc={page.data.toc} full={page.data.full}>
      <DocsTitle>{page.data.title}</DocsTitle>
      <DocsDescription>{page.data.description}</DocsDescription>
      <DocsBody>
        <MDX
          components={getMDXComponents({
            a: createRelativeLink(source, page),
          })}
        />
      </DocsBody>
    </DocsPage>
  );
}

/**
 * Builds the browser tab title in the main application's format:
 * brand prefix + section dot leaf — `plexor / docs.marketplace`. Docs add
 * their own `docs` section so the tab never masquerades as an app page. The
 * RU index keeps the bare `plexor / docs` (no leaf — brand doubling reads
 * badly); locale pages stay ASCII via their URL slug, not the localized
 * title.
 */
function tabTitle(pageTitle: string | undefined, locale: string, slug: string[]): string | undefined {
  if (pageTitle === undefined || pageTitle.length === 0) {
    return undefined;
  }

  if (slug.length === 0) {
    return `plexor / docs${locale === 'en' ? '.en' : ''}`;
  }

  return `plexor / docs.${slug.join('.')}`;
}

export function docsPageMetadata(page: DocsPage, locale: string): Metadata {
  return {
    title: tabTitle(page.data.title, locale, page.slugs),
    description: page.data.description,
  };
}