import { createFileRoute } from '@tanstack/react-router';
import { getMdxComponents } from '@/lib/mdx-components';
import MdxContent from './content.mdx';

/**
 * `/docs/getting-started/install/` route — loads the sibling content.mdx
 * file and renders it inside the docs chrome. The install page is the
 * first tutorial in the Getting started chapter; it lands the operator
 * in front of a running Plexor host ready to take a first login.
 */
export const Route = createFileRoute('/(docs)/docs/getting-started/install/')({
  component: Page,
  head: () => ({
    meta: [
      { title: 'Install Plexor' },
      {
        name: 'description',
        content:
          'Two supported paths to a running Plexor host — bootable ISO for bare metal, and plx init for an existing Linux host.',
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