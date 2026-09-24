import { createFileRoute } from '@tanstack/react-router';
import { getMdxComponents } from '@/lib/mdx-components';
import MdxContent from './content.mdx';

/**
 * `/docs/reference/` route — loads the sibling content.mdx file and
 * renders it inside the docs chrome.
 */
export const Route = createFileRoute('/(docs)/docs/reference/')({
  component: Page,
  head: () => ({
    meta: [
      { title: 'Reference' },
      {
        name: 'description',
        content: 'Exhaustive catalogs — APIs, permissions, quotas, vocabulary.',
      },
    ],
  }),
});

function Page() {
  const components = getMdxComponents();
  return (
    <article className="docs-prose">
      <MdxContent components={components} />
    </article>
  );
}