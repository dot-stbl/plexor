import { createFileRoute } from '@tanstack/react-router';
import { getMdxComponents } from '@/lib/mdx-components';
import MdxContent from './content.mdx';

/**
 * `/docs/how-to/create-bucket/` route — loads the sibling content.mdx file and
 * renders it inside the docs chrome.
 */
export const Route = createFileRoute('/(docs)/docs/how-to/create-bucket/')({
  component: Page,
  head: () => ({
    meta: [
      { title: "Create a bucket" },
      {
        name: 'description',
        content: "Provision an S3-compatible bucket, with the access-key copy flow.",
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