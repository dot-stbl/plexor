import { createFileRoute } from '@tanstack/react-router';
import { getMdxComponents } from '@/lib/mdx-components';
import MdxContent from './content.mdx';

/**
 * `/docs/how-to/customize-branding/` route — loads the sibling content.mdx file and
 * renders it inside the docs chrome.
 */
export const Route = createFileRoute('/(docs)/docs/how-to/customize-branding/')({
  component: Page,
  head: () => ({
    meta: [
      { title: "Customize the console's brand — plexor docs" },
      {
        name: 'description',
        content: "Set the name, logo, accent colour, and custom CSS — with the live preview.",
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