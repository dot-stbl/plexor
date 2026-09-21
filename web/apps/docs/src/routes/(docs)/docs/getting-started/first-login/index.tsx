import { createFileRoute } from '@tanstack/react-router';
import { getMdxComponents } from '@/lib/mdx-components';
import MdxContent from './content.mdx';

/**
 * `/docs/getting-started/first-login/` route — loads the sibling content.mdx file and
 * renders it inside the docs chrome.
 */
export const Route = createFileRoute('/(docs)/docs/getting-started/first-login/')({
  component: Page,
  head: () => ({
    meta: [
      { title: "First login — plexor docs" },
      {
        name: 'description',
        content: "Get the operator into the console for the first time and set the admin password.",
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