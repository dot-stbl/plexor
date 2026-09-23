import { createFileRoute } from '@tanstack/react-router';
import { getMdxComponents } from '@/lib/mdx-components';
import MdxContent from './content.mdx';

/**
 * `/docs/getting-started/first-api-key/` route — loads the sibling content.mdx file and
 * renders it inside the docs chrome.
 */
export const Route = createFileRoute('/(docs)/docs/getting-started/first-api-key/')({
  component: Page,
  head: () => ({
    meta: [
      { title: "Your first API key" },
      {
        name: 'description',
        content: "Issue an API key for a service account and call a resource over HTTP.",
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