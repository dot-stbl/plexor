import { createFileRoute } from '@tanstack/react-router';
import { getMdxComponents } from '@/lib/mdx-components';
import MdxContent from './content.mdx';

/**
 * `/docs/concepts/compute-and-workloads/` route — loads the sibling content.mdx file and
 * renders it inside the docs chrome.
 */
export const Route = createFileRoute('/(docs)/docs/concepts/compute-and-workloads/')({
  component: Page,
  head: () => ({
    meta: [
      { title: "Workloads and runtimes" },
      {
        name: 'description',
        content: "What a workload is, which runtimes ship, and what the lifecycle states mean.",
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