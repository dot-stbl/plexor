import { createFileRoute } from '@tanstack/react-router';
import { getMdxComponents } from '@/lib/mdx-components';
import MdxContent from './content.mdx';

/**
 * `/docs/how-to/reserve-floating-ip/` route — loads the sibling content.mdx file and
 * renders it inside the docs chrome.
 */
export const Route = createFileRoute('/(docs)/docs/how-to/reserve-floating-ip/')({
  component: Page,
  head: () => ({
    meta: [
      { title: "Reserve a floating IP — plexor docs" },
      {
        name: 'description',
        content: "Reserve, assign, reassign, and release a floating IP.",
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