import { createFileRoute } from '@tanstack/react-router';
import { getMdxComponents } from '@/lib/mdx-components';
import MdxContent from './content.mdx';

/**
 * `/docs/admin/quotas/` route — loads the sibling content.mdx file and
 * renders it inside the docs chrome.
 */
export const Route = createFileRoute('/(docs)/docs/admin/quotas/')({
  component: Page,
  head: () => ({
    meta: [
      { title: "Managing quotas" },
      {
        name: 'description',
        content: "Where to set a folder override, what the 80% warning looks like in the UI, and why there is no admin bypass.",
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