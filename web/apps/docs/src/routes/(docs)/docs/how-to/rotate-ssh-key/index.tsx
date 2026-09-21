import { createFileRoute } from '@tanstack/react-router';
import { getMdxComponents } from '@/lib/mdx-components';
import MdxContent from './content.mdx';

/**
 * `/docs/how-to/rotate-ssh-key/` route — loads the sibling content.mdx file and
 * renders it inside the docs chrome.
 */
export const Route = createFileRoute('/(docs)/docs/how-to/rotate-ssh-key/')({
  component: Page,
  head: () => ({
    meta: [
      { title: "Rotate an SSH key — plexor docs" },
      {
        name: 'description',
        content: "Add a new SSH key, fingerprint-deduped; remove the old one; the LastUsedAt debounce.",
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