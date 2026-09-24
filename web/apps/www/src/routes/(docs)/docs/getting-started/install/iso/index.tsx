import { createFileRoute } from '@tanstack/react-router';
import { getMdxComponents } from '@/lib/mdx-components';
import MdxContent from './content.mdx';

/**
 * `/docs/getting-started/install/iso/` route — bootable ISO install
 * path. This page is a stub; the full procedure (downloading the
 * image, flashing to USB, booting, walking the wizard) lands in a
 * follow-up. For now it points operators at the universal install
 * page so the install flow stays navigable end-to-end.
 */
export const Route = createFileRoute(
  '/(docs)/docs/getting-started/install/iso/',
)({
  component: Page,
  head: () => ({
    meta: [
      { title: 'Install Plexor via bootable ISO' },
      {
        name: 'description',
        content:
          'Flash the Plexor ISO to a USB stick, boot the target machine, and walk the installer wizard.',
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