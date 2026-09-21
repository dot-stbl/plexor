import { notFound } from 'next/navigation';
import { DocsPageView, docsPageMetadata } from '@/components/docs-page-view';
import { source } from '@/lib/source';
import type { Metadata } from 'next';

/** RU index page — internal `/`, public `/docs/`. */
export default async function RuIndexPage() {
  const page = source.getPage([], 'ru');

  if (!page) {
    notFound();
  }

  return <DocsPageView page={page} />;
}

export function generateMetadata(): Metadata {
  const page = source.getPage([], 'ru');

  if (!page) {
    return {};
  }

  return docsPageMetadata(page, 'ru');
}

