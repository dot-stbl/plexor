import { createFileRoute } from '@tanstack/react-router';
import { getMdxComponents } from '@/lib/mdx-components';
import MdxContent from './content.mdx';

/**
 * `/docs/how-to/issue-api-key/` route — loads the sibling content.mdx file and
 * renders it inside the docs chrome.
 */
export const Route = createFileRoute('/(docs)/docs/how-to/issue-api-key/')({
  component: Page,
  head: () => ({
    meta: [
      { title: "Issue an API key" },
      {
        name: 'description',
        content: "Issue an API key for a NodeAgent or other service account, with the copy-once flow and the kid_&lt;id&gt;.&lt;secret&gt; bearer shape.",
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