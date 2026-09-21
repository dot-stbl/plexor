import { createFileRoute } from '@tanstack/react-router';
import { getMdxComponents } from '@/lib/mdx-components';
import MdxContent from './content.mdx';

/**
 * `/docs/reference/glossary/` route — loads the sibling content.mdx file and
 * renders it inside the docs chrome.
 */
export const Route = createFileRoute('/(docs)/docs/reference/glossary/')({
  component: Page,
  head: () => ({
    meta: [
      { title: "Glossary — plexor docs" },
      {
        name: 'description',
        content: "Operator-facing definitions for every term Plexor uses in the UI and the documentation.",
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