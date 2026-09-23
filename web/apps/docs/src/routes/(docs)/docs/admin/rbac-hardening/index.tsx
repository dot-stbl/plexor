import { createFileRoute } from '@tanstack/react-router';
import { getMdxComponents } from '@/lib/mdx-components';
import MdxContent from './content.mdx';

/**
 * `/docs/admin/rbac-hardening/` route — loads the sibling content.mdx file and
 * renders it inside the docs chrome.
 */
export const Route = createFileRoute('/(docs)/docs/admin/rbac-hardening/')({
  component: Page,
  head: () => ({
    meta: [
      { title: "RBAC hardening" },
      {
        name: 'description',
        content: "Why wildcards are dangerous, how to write a least-privilege role for a NodeAgent integration, and why permission changes don't take effect on already-issued JWTs.",
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