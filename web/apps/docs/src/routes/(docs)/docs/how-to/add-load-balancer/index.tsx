import { createFileRoute } from '@tanstack/react-router';
import { getMdxComponents } from '@/lib/mdx-components';
import MdxContent from './content.mdx';

/**
 * `/docs/how-to/add-load-balancer/` route — loads the sibling content.mdx file and
 * renders it inside the docs chrome.
 */
export const Route = createFileRoute('/(docs)/docs/how-to/add-load-balancer/')({
  component: Page,
  head: () => ({
    meta: [
      { title: "Add a load balancer — plexor docs" },
      {
        name: 'description',
        content: "Round-robin vs least-connections — pick an algorithm, add targets, and confirm Active status.",
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