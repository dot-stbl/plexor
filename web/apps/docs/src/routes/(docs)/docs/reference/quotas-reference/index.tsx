import { createFileRoute } from '@tanstack/react-router';
import { getMdxComponents } from '@/lib/mdx-components';
import MdxContent from './content.mdx';

/**
 * `/docs/reference/quotas-reference/` route — loads the sibling content.mdx file and
 * renders it inside the docs chrome.
 */
export const Route = createFileRoute('/(docs)/docs/reference/quotas-reference/')({
  component: Page,
  head: () => ({
    meta: [
      { title: "Quotas reference — plexor docs" },
      {
        name: 'description',
        content: "The catalog keys, the default values, the scope-resolution walker, and the two signals (warning at 80%, denial at 100%).",
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