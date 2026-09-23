import { createFileRoute } from '@tanstack/react-router';
import { getMdxComponents } from '@/lib/mdx-components';
import MdxContent from './content.mdx';

/**
 * `/docs/concepts/quotas/` route — loads the sibling content.mdx file and
 * renders it inside the docs chrome.
 */
export const Route = createFileRoute('/(docs)/docs/concepts/quotas/')({
  component: Page,
  head: () => ({
    meta: [
      { title: "Quotas" },
      {
        name: 'description',
        content: "Why quotas exist, how folder → team → org resolution walks the hierarchy, the 80% warning signal, and the catalog keys that ship in the current release.",
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