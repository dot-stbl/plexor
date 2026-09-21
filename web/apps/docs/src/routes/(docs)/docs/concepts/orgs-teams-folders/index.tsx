import { createFileRoute } from '@tanstack/react-router';
import { getMdxComponents } from '@/lib/mdx-components';
import MdxContent from './content.mdx';

/**
 * `/docs/concepts/orgs-teams-folders/` route — loads the sibling content.mdx file and
 * renders it inside the docs chrome.
 */
export const Route = createFileRoute('/(docs)/docs/concepts/orgs-teams-folders/')({
  component: Page,
  head: () => ({
    meta: [
      { title: "Orgs, teams, and folders — plexor docs" },
      {
        name: 'description',
        content: "The 3-tier scope hierarchy that decides who can see and write to what.",
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