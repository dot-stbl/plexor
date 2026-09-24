import { createFileRoute } from '@tanstack/react-router';
import { getMdxComponents } from '@/lib/mdx-components';
import MdxContent from './content.mdx';

/**
 * `/docs/admin/backup-and-disaster/` route — loads the sibling content.mdx file and
 * renders it inside the docs chrome.
 */
export const Route = createFileRoute('/(docs)/docs/admin/backup-and-disaster/')({
  component: Page,
  head: () => ({
    meta: [
      { title: 'Backup and disaster avoidance' },
      {
        name: 'description',
        content:
          "What Plexor persists by design — the control-plane database and an on-disk secrets root — what it doesn't (workload disks and bucket data), and how to back up and restore both.",
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
