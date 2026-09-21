import { createFileRoute } from '@tanstack/react-router';
import { getMdxComponents } from '@/lib/mdx-components';
import MdxContent from './content.mdx';

/**
 * `/docs/admin/lockout-recovery/` route — loads the sibling content.mdx file and
 * renders it inside the docs chrome.
 */
export const Route = createFileRoute('/(docs)/docs/admin/lockout-recovery/')({
  component: Page,
  head: () => ({
    meta: [
      { title: "Lockout recovery — plexor docs" },
      {
        name: 'description',
        content: "The 5/10/15 thresholds and the 15-minute/1-hour/24-hour lockout windows, with the manual DB unlock path for break-glass.",
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