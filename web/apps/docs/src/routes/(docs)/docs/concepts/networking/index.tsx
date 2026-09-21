import { createFileRoute } from '@tanstack/react-router';
import { getMdxComponents } from '@/lib/mdx-components';
import MdxContent from './content.mdx';

/**
 * `/docs/concepts/networking/` route — loads the sibling content.mdx file and
 * renders it inside the docs chrome.
 */
export const Route = createFileRoute('/(docs)/docs/concepts/networking/')({
  component: Page,
  head: () => ({
    meta: [
      { title: "Networking, floating IPs, and load balancers — plexor docs" },
      {
        name: 'description',
        content: "VPCs, subnets, floating IPs, and load balancers — what the operator can attach and how the pieces fit together.",
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