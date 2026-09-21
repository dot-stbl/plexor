import { createFileRoute } from '@tanstack/react-router';
import { getMdxComponents } from '@/lib/mdx-components';
import MdxContent from './content.mdx';

/**
 * `/docs/reference/permissions/` route — loads the sibling content.mdx file and
 * renders it inside the docs chrome.
 */
export const Route = createFileRoute('/(docs)/docs/reference/permissions/')({
  component: Page,
  head: () => ({
    meta: [
      { title: "Permissions catalog — plexor docs" },
      {
        name: 'description',
        content: "Every permission string Plexor ships, with the resource and action each guards, and the role the platform seeds it into.",
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