import { createFileRoute } from '@tanstack/react-router';
import { getMdxComponents } from '@/lib/mdx-components';
import MdxContent from './content.mdx';

/**
 * `/docs/faq/troubleshooting/` route — loads the sibling content.mdx file and
 * renders it inside the docs chrome.
 */
export const Route = createFileRoute('/(docs)/docs/faq/troubleshooting/')({
  component: Page,
  head: () => ({
    meta: [
      { title: "Troubleshooting recipes — plexor docs" },
      {
        name: 'description',
        content: "Symptom → cause → fix for the things operators hit most often.",
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