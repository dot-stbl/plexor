import { createFileRoute } from '@tanstack/react-router';

/**
 * Getting started — the first page every operator reads. Deliberately
 * a TSX file rather than MDX: this is chrome-validation content, not a
 * long-form article. Future chapter pages (concepts, marketplace,
 * operations, reference) keep their MDX files unchanged; only the
 * placeholder chapter that proves the sidebar works as TSX.
 *
 * Two-paragraph framing + a small "what's next" pointer at the
 * bottom. No badges, no images, no code blocks — copy edits only.
 */
export const Route = createFileRoute('/(docs)/docs/getting-started')({
  component: GettingStartedPage,
  head: () => ({
    meta: [
      { title: 'Getting started — plexor docs' },
      {
        name: 'description',
        content:
          'First login, plx CLI, app structure — the minimum to put Plexor into service.',
      },
    ],
  }),
});

function GettingStartedPage() {
  return (
    <article className="docs-prose">
      <p className="mb-2 font-mono text-[10px] font-medium uppercase tracking-[0.16em] text-muted-2">
        Chapter 01 · Getting started
      </p>
      <h1 className="mb-4 mt-0 text-3xl font-semibold tracking-tight text-foreground">
        Getting started
      </h1>

      <p className="my-4 text-base leading-7 text-foreground">
        Plexor is a self-hosted cloud platform. The v0.2 binary ships a
        single <code>Plexor.Host</code> process with the Tenants,
        Identity, Audit, Compute, Network, and Storage modules wired in.
        You boot it, you log in, you create a tenant — the rest of the
        system follows from the hierarchy documented under{' '}
        <a
          href="/docs/concepts"
          className="text-foreground underline decoration-border-2 underline-offset-4 hover:decoration-foreground"
        >
          Concepts
        </a>
        .
      </p>

      <p className="my-4 text-base leading-7 text-foreground">
        The CLI (<code>plx</code>) is the operator's primary surface in
        v0.x. The console is the tenant's surface. The two don't
        overlap: <code>plx init</code> probes your hardware and writes
        a single-node manifest, <code>plx provider install</code>{' '}
        installs a community app provider from a <code>provider.yaml</code>{' '}
        manifest, and the console only sees what the binary has
        already provisioned.
      </p>

      <h2 className="mb-3 mt-10 text-2xl font-semibold tracking-tight text-foreground">
        What to read next
      </h2>

      <ul className="my-4 ml-6 list-disc space-y-1.5 text-foreground marker:text-muted-2">
        <li className="leading-7">
          <a
            href="/docs/concepts"
            className="text-foreground underline decoration-border-2 underline-offset-4 hover:decoration-foreground"
          >
            Concepts
          </a>{' '}
          — the mental model (organisation, team, folder) and the
          resource scope hierarchy.
        </li>
        <li className="leading-7">
          The marketplace, operations, and reference chapters land as
          their pages are authored. Each one is one tree-level deep in
          the sidebar.
        </li>
      </ul>
    </article>
  );
}