import { createFileRoute } from '@tanstack/react-router';
import { getMdxComponents } from '@/lib/mdx-components';
import MdxContent from './content.mdx';

/**
 * `/docs/how-to/configure-oidc/` route — loads the sibling content.mdx file and
 * renders it inside the docs chrome.
 */
export const Route = createFileRoute('/(docs)/docs/how-to/configure-oidc/')({
  component: Page,
  head: () => ({
    meta: [
      { title: "Configure OIDC for an organisation — plexor docs" },
      {
        name: 'description',
        content: "Wire an external OIDC identity provider (Keycloak, Authentik, Azure AD) per organisation, with the discovery-document test endpoint and * admin retention.",
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