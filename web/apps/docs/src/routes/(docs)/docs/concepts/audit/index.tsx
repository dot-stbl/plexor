import { createFileRoute } from '@tanstack/react-router';
import { getMdxComponents } from '@/lib/mdx-components';
import MdxContent from './content.mdx';

/**
 * `/docs/concepts/audit/` route — loads the sibling content.mdx file and
 * renders it inside the docs chrome.
 */
export const Route = createFileRoute('/(docs)/docs/concepts/audit/')({
  component: Page,
  head: () => ({
    meta: [
      { title: "Audit log — plexor docs" },
      {
        name: 'description',
        content: "Every state-changing call writes an entry — what the columns mean, how to query, and the retention window.",
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