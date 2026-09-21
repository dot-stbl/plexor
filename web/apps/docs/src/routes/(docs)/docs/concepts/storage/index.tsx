import { createFileRoute } from '@tanstack/react-router';
import { getMdxComponents } from '@/lib/mdx-components';
import MdxContent from './content.mdx';

/**
 * `/docs/concepts/storage/` route — loads the sibling content.mdx file and
 * renders it inside the docs chrome.
 */
export const Route = createFileRoute('/(docs)/docs/concepts/storage/')({
  component: Page,
  head: () => ({
    meta: [
      { title: "Storage: volumes and buckets — plexor docs" },
      {
        name: 'description',
        content: "Block volumes vs S3 buckets — sizing, attachment, and when to use which.",
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