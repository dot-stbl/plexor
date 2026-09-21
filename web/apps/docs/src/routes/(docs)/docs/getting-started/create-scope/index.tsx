import { createFileRoute } from '@tanstack/react-router';
import { getMdxComponents } from '@/lib/mdx-components';
import MdxContent from './content.mdx';

/**
 * `/docs/getting-started/create-scope/` route — loads the sibling content.mdx file and
 * renders it inside the docs chrome.
 */
export const Route = createFileRoute('/(docs)/docs/getting-started/create-scope/')({
  component: Page,
  head: () => ({
    meta: [
      { title: "Create your first org, team, and folder — plexor docs" },
      {
        name: 'description',
        content: "Walk through the three-tier scope hierarchy interactively.",
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