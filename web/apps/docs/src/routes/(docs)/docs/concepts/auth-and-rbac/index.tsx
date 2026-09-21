import { createFileRoute } from '@tanstack/react-router';
import { getMdxComponents } from '@/lib/mdx-components';
import MdxContent from './content.mdx';

/**
 * `/docs/concepts/auth-and-rbac/` route — loads the sibling content.mdx file and
 * renders it inside the docs chrome.
 */
export const Route = createFileRoute('/(docs)/docs/concepts/auth-and-rbac/')({
  component: Page,
  head: () => ({
    meta: [
      { title: "Authentication and RBAC — plexor docs" },
      {
        name: 'description',
        content: "How Plexor authenticates a request — local users, OIDC, JWT claims, API keys, and the permission catalog.",
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