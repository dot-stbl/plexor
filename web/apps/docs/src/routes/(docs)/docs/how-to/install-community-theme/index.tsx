import { createFileRoute } from '@tanstack/react-router';
import { getMdxComponents } from '@/lib/mdx-components';
import MdxContent from './content.mdx';

/**
 * `/docs/how-to/install-community-theme/` route — loads the sibling content.mdx file and
 * renders it inside the docs chrome.
 */
export const Route = createFileRoute('/(docs)/docs/how-to/install-community-theme/')({
  component: Page,
  head: () => ({
    meta: [
      { title: "Install and activate a community theme — plexor docs" },
      {
        name: 'description',
        content: "Pick a community theme from the marketplace, install it, and activate it for the console.",
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