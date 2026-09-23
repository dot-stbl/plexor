import { createFileRoute } from '@tanstack/react-router';
import { getMdxComponents } from '@/lib/mdx-components';
import MdxContent from './content.mdx';

/**
 * `/docs/admin/theming-console/` route — loads the sibling content.mdx file and
 * renders it inside the docs chrome.
 */
export const Route = createFileRoute('/(docs)/docs/admin/theming-console/')({
  component: Page,
  head: () => ({
    meta: [
      { title: 'Theming the console' },
      {
        name: 'description',
        content:
          'Set the brand name, logo and accent colour, or apply a community theme from the marketplace.',
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
