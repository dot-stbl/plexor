import { createFileRoute } from '@tanstack/react-router';
import { getMdxComponents } from '@/lib/mdx-components';
import ConceptsContent from './content.mdx';

/**
 * /concepts route — loads the `concepts/content.mdx` Markdown and
 * renders it inside the docs chrome. The MDX page declares its own
 * headings (`#`, `##`) — `getMdxComponents()` provides the typed
 * components the MDX provider uses when expanding the file.
 */
export const Route = createFileRoute('/concepts/')({
  component: ConceptsPage,
  head: () => ({
    meta: [
      { title: 'Concepts — plexor docs' },
      {
        name: 'description',
        content:
          'The Plexor mental model — organisation, team, folder, and the resource scope hierarchy.',
      },
    ],
  }),
});

function ConceptsPage() {
  const components = getMdxComponents();
  return (
    <article className="docs-prose">
      <ConceptsContent components={components} />
    </article>
  );
}