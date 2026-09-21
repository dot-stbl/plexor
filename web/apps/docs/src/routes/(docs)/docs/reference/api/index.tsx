import { createFileRoute } from '@tanstack/react-router';
import { getMdxComponents } from '@/lib/mdx-components';
import MdxContent from './content.mdx';

/**
 * `/docs/reference/api/` route — loads the sibling content.mdx file and
 * renders it inside the docs chrome.
 */
export const Route = createFileRoute('/(docs)/docs/reference/api/')({
  component: Page,
  head: () => ({
    meta: [
      { title: "REST API reference — plexor docs" },
      {
        name: 'description',
        content: "The /api/v1 surface — endpoint groups by capability, request shapes, response shapes, and stable error codes.",
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