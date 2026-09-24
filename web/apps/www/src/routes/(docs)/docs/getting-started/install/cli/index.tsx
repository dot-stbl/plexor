import { createFileRoute } from '@tanstack/react-router';
import { getMdxComponents } from '@/lib/mdx-components';
import MdxContent from './content.mdx';

/**
 * `/docs/getting-started/install/cli/` route — `plx init` install
 * path on an existing Linux host. This page is a stub; the full
 * procedure lands in a follow-up. For now it covers the universal
 * install flow and points operators back to the canonical page.
 */
export const Route = createFileRoute(
  '/(docs)/docs/getting-started/install/cli/',
)({
  component: Page,
  head: () => ({
    meta: [
      { title: 'Install Plexor via plx init' },
      {
        name: 'description',
        content:
          'Install Plexor on an existing Ubuntu or AlmaLinux host using the plx CLI.',
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