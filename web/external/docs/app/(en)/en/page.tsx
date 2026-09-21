import { notFound } from 'next/navigation';
import { DocsPageView, docsPageMetadata } from '@/components/docs-page-view';
import { source } from '@/lib/source';
import type { Metadata } from 'next';

/** EN index page — internal `/en`, public `/docs/en/`. */
export default async function EnIndexPage() {
  const page = source.getPage([], 'en');

  if (!page) {
    notFound();
  }

  return <DocsPageView page={page} />;
}

export function generateMetadata(): Metadata {
  const page = source.getPage([], 'en');

  if (!page) {
    return {};
  }

  return docsPageMetadata(page, 'en');
}

