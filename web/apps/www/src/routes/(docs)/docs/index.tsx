import { createFileRoute } from '@tanstack/react-router';
import { DocsLanding } from '@/components/docs/docs-landing';

/**
 * `/docs` — `/(docs)` pathless group's direct index. A real entry page
 * (YC-informed restyle, 2026-09-24): a hero panel + chapter-cards grid
 * (`DocsLanding`), replacing the old immediate `beforeLoad` redirect to
 * `/docs/getting-started`. Every other docs route
 * (`/docs/getting-started`, `/docs/concepts`, …) is unaffected — this
 * file only changes what renders at the bare `/docs`/`/docs/` path.
 */
export const Route = createFileRoute('/(docs)/docs/')({
  component: DocsIndexPage,
  head: () => ({
    meta: [
      { title: 'plexor — documentation' },
      {
        name: 'description',
        content: 'Install, run and operate Plexor — from first boot to hardening a production cluster.',
      },
    ],
  }),
});

function DocsIndexPage() {
  return <DocsLanding />;
}
