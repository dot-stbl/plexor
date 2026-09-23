import { createFileRoute } from '@tanstack/react-router';
import { getMdxComponents } from '@/lib/mdx-components';
import MdxContent from './content.mdx';

/**
 * `/docs/how-to/manage-workload-lifecycle/` route — loads the sibling content.mdx file and
 * renders it inside the docs chrome.
 */
export const Route = createFileRoute('/(docs)/docs/how-to/manage-workload-lifecycle/')({
  component: Page,
  head: () => ({
    meta: [
      { title: "Workload lifecycle" },
      {
        name: 'description',
        content: "The four buttons every workload exposes — start, stop, restart, delete — and what each one does to the state machine.",
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