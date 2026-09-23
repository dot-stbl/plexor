import { createFileRoute } from '@tanstack/react-router';
import { getMdxComponents } from '@/lib/mdx-components';
import MdxContent from './content.mdx';

/**
 * `/docs/admin/audit-log/` route — loads the sibling content.mdx file and
 * renders it inside the docs chrome.
 */
export const Route = createFileRoute('/(docs)/docs/admin/audit-log/')({
  component: Page,
  head: () => ({
    meta: [
      { title: "Reading the audit log" },
      {
        name: 'description',
        content: "The four query parameters, the X-Quota-Warning rationale, and the 90-day retention window.",
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