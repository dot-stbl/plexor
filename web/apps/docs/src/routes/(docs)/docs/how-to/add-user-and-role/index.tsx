import { createFileRoute } from '@tanstack/react-router';
import { getMdxComponents } from '@/lib/mdx-components';
import MdxContent from './content.mdx';

/**
 * `/docs/how-to/add-user-and-role/` route — loads the sibling content.mdx file and
 * renders it inside the docs chrome.
 */
export const Route = createFileRoute('/(docs)/docs/how-to/add-user-and-role/')({
  component: Page,
  head: () => ({
    meta: [
      { title: "Add a user and assign a role — plexor docs" },
      {
        name: 'description',
        content: "Invite a user, choose a built-in or custom role, and bind the role to the right scope.",
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