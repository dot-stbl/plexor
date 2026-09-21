import { createFileRoute } from '@tanstack/react-router';
import { getMdxComponents } from '@/lib/mdx-components';
import MdxContent from './content.mdx';

/**
 * `/docs/admin/capacity-planning/` route — loads the sibling content.mdx file and
 * renders it inside the docs chrome.
 */
export const Route = createFileRoute('/(docs)/docs/admin/capacity-planning/')({
  component: Page,
  head: () => ({
    meta: [
      { title: "Capacity planning — plexor docs" },
      {
        name: 'description',
        content: "Default org-seeded quotas, when to raise the org versus adding a folder override, and what to watch on dashboards.",
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