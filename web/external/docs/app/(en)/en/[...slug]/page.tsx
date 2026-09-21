import { notFound } from 'next/navigation';
import { DocsPageView, docsPageMetadata } from '@/components/docs-page-view';
import { source } from '@/lib/source';
import type { Metadata } from 'next';

type PageProps = { params: Promise<{ slug: string[] }> };

/** EN content pages — internal `/en/{slug...}`, public `/docs/en/{slug...}/`. */
export default async function EnDocsPage({ params }: PageProps) {
  const { slug } = await params;
  const page = source.getPage(slug, 'en');

  if (!page) {
    notFound();
  }

  return <DocsPageView page={page} />;
}

export function generateStaticParams() {
  return source
    .getPages('en')
    .filter((page) => page.slugs.length > 0)
    .map((page) => ({ slug: page.slugs }));
}

export async function generateMetadata({ params }: PageProps): Promise<Metadata> {
  const { slug } = await params;
  const page = source.getPage(slug, 'en');

  if (!page) {
    return {};
  }

  return docsPageMetadata(page, 'en');
}

