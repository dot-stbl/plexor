import { createFileRoute } from '@tanstack/react-router';
import { getMdxComponents } from '@/lib/mdx-components';
import MdxContent from './content.mdx';

/**
 * `/docs/how-to/create-workload/` route — loads the sibling content.mdx file and
 * renders it inside the docs chrome.
 */
export const Route = createFileRoute('/(docs)/docs/how-to/create-workload/')({
  component: Page,
  head: () => ({
    meta: [
      { title: "Create a workload" },
      {
        name: 'description',
        content: "Provision a VM, container, or pod through the console, with the equivalent API call.",
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