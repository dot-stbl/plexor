import { createFileRoute } from '@tanstack/react-router';
import { getMdxComponents } from '@/lib/mdx-components';
import MdxContent from './content.mdx';

/**
 * `/docs/getting-started/install/airgap/` route — air-gapped bundle
 * install path. This page is a stub; the full bundle procedure
 * lands in a follow-up. For now it covers the universal install
 * flow and points operators back to the canonical page.
 */
export const Route = createFileRoute(
  '/(docs)/docs/getting-started/install/airgap/',
)({
  component: Page,
  head: () => ({
    meta: [
      { title: 'Install Plexor via air-gapped bundle' },
      {
        name: 'description',
        content:
          'Download a Plexor bundle, transfer it across the air gap, and run the local installer.',
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