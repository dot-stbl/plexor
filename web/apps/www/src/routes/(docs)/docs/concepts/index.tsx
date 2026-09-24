import { createFileRoute } from '@tanstack/react-router';
import { getMdxComponents } from '@/lib/mdx-components';
import MdxContent from './content.mdx';

/**
 * `/docs/concepts/` route — loads the sibling content.mdx file and
 * renders it inside the docs chrome.
 */
export const Route = createFileRoute('/(docs)/docs/concepts/')({
  component: Page,
  head: () => ({
    meta: [
      { title: "Concepts" },
      {
        name: 'description',
        content: "Mental models for working with Plexor — the resource scope hierarchy, identity and RBAC, workloads and runtimes, networking, storage, quotas, and the audit log.",
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