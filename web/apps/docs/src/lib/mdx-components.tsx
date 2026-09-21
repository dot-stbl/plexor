import type { MDXComponents } from 'mdx/types';
import type { ComponentProps } from 'react';

/**
 * MDX components — the docs site reuses the Plexor DS prose typography
 * (h1-h4, p, ul/ol, code, blockquote) from a small in-app primitive set.
 * These primitives read theme tokens (`text-foreground`, `border-border`,
 * `bg-surface-2`, …) so headings, code blocks, and quotes inherit the
 * active preset without bespoke colour declarations.
 *
 * Future work: extract these into `@plexor/ui/mdx` once a second
 * consumer (a blog, a status page) makes it worthwhile; for v1 the
 * primitives live next to the docs site that consumes them.
 */
export interface DocsHeadingProps extends ComponentProps<'h2'> {
  readonly children?: React.ReactNode;
}

function h1(props: DocsHeadingProps) {
  return (
    <h1
      {...props}
      className="mb-4 mt-0 scroll-mt-20 text-3xl font-semibold tracking-tight text-foreground"
    />
  );
}

function h2(props: DocsHeadingProps) {
  return (
    <h2
      {...props}
      className="mb-3 mt-10 scroll-mt-20 text-2xl font-semibold tracking-tight text-foreground"
    />
  );
}

function h3(props: DocsHeadingProps) {
  return (
    <h3
      {...props}
      className="mb-2 mt-8 scroll-mt-20 text-xl font-semibold tracking-tight text-foreground"
    />
  );
}

function h4(props: DocsHeadingProps) {
  return (
    <h4
      {...props}
      className="mb-2 mt-6 scroll-mt-20 text-base font-semibold tracking-tight text-foreground"
    />
  );
}

function p(props: ComponentProps<'p'>) {
  return (
    <p
      {...props}
      className="my-4 text-base leading-7 text-foreground [&:not(:first-child)]:mt-4"
    />
  );
}

function ul(props: ComponentProps<'ul'>) {
  return (
    <ul
      {...props}
      className="my-4 ml-6 list-disc space-y-1.5 text-foreground marker:text-muted-2"
    />
  );
}

function ol(props: ComponentProps<'ol'>) {
  return (
    <ol
      {...props}
      className="my-4 ml-6 list-decimal space-y-1.5 text-foreground marker:text-muted-2"
    />
  );
}

function li(props: ComponentProps<'li'>) {
  return <li {...props} className="leading-7" />;
}

function blockquote(props: ComponentProps<'blockquote'>) {
  return (
    <blockquote
      {...props}
      className="my-4 border-l-2 border-border-2 pl-4 text-muted-foreground [&>p]:my-2"
    />
  );
}

function code(props: ComponentProps<'code'>) {
  return (
    <code
      {...props}
      className="rounded border border-border bg-surface-2 px-1.5 py-0.5 font-mono text-[0.875em] text-foreground"
    />
  );
}

function pre(props: ComponentProps<'pre'>) {
  return (
    <pre
      {...props}
      className="my-4 overflow-x-auto rounded-lg border border-border bg-surface-2 p-4 font-mono text-sm leading-6 text-foreground"
    />
  );
}

function hr(props: ComponentProps<'hr'>) {
  return <hr {...props} className="my-8 border-border" />;
}

function a(props: ComponentProps<'a'>) {
  return (
    <a
      {...props}
      className="text-foreground underline decoration-border-2 underline-offset-4 hover:decoration-foreground"
    />
  );
}

export function getMdxComponents(): MDXComponents {
  return {
    h1,
    h2,
    h3,
    h4,
    p,
    ul,
    ol,
    li,
    blockquote,
    code,
    pre,
    hr,
    a,
  };
}