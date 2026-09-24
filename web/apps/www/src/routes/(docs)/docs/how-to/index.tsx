import { createFileRoute } from '@tanstack/react-router';
import { getMdxComponents } from '@/lib/mdx-components';
import MdxContent from './content.mdx';

/**
 * `/docs/how-to/` route — loads the sibling content.mdx file and
 * renders it inside the docs chrome.
 */
export const Route = createFileRoute('/(docs)/docs/how-to/')({
  component: Page,
  head: () => ({
    meta: [
      { title: 'How-to' },
      {
        name: 'description',
        content: 'Step-by-step recipes for everyday Plexor tasks.',
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