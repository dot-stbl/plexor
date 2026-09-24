import type { MDXComponents } from 'mdx/types';
import type { ComponentProps } from 'react';
import {
  Accordion,
  Accordions,
  Callout,
  Choice,
  Choices,
  Diagram,
  DiagramPlaceholder,
  Kbd,
  PlatformMatrix,
  Screenshot,
  ScreenshotPlaceholder,
  Step,
  TypeRow,
  TypeTable,
} from '@/components/mdx';
import { CopyButton } from '@/components/ui/copy-button';
import { extractCodeText } from '@/components/docs/docs-code-text';

/**
 * MDX components — the docs site reuses the Plexor DS prose typography
 * (h1-h4, p, ul/ol, code, blockquote) from a small in-app primitive set,
 * reading theme tokens (`text-foreground`, `border-border`, `bg-surface-2`,
 * …) so headings/code/quotes inherit the active preset with no bespoke
 * colour declarations. `pre` additionally mounts a `CopyButton` per code
 * block, reading the raw text back out via `extractCodeText`.
 *
 * The MDX-only components (`Callout`, `Step`, `Screenshot`, `Kbd`,
 * `Diagram`, `PlatformMatrix`, `Choices`, …) are registered globally so
 * every MDX file can use them with no explicit import — authors write
 * `<Callout type="warning">`, not an import statement. Future work: extract
 * these into `@plexor/ui/mdx` once a second consumer justifies it.
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
  const codeText = extractCodeText(props.children);
  return (
    <div className="relative my-4">
      <pre
        {...props}
        className="overflow-x-auto rounded-lg border border-border bg-surface-2 p-4 font-mono text-sm leading-6 text-foreground"
      />
      {codeText.length > 0 && (
        <CopyButton
          value={codeText}
          copyLabel="Copy code"
          className="absolute right-2 top-2 rounded-md border border-border bg-card"
        />
      )}
    </div>
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

function table(props: ComponentProps<'table'>) {
  return (
    <div className="my-6 overflow-x-auto">
      <table
        {...props}
        className="w-full border-collapse text-sm [&_th]:py-2 [&_th]:pr-4 [&_th]:text-left [&_th]:font-mono [&_th]:text-[10px] [&_th]:font-medium [&_th]:uppercase [&_th]:tracking-[0.14em] [&_th]:text-muted-2 [&_td]:py-2 [&_td]:pr-4 [&_td]:align-top [&_td]:text-foreground [&_tr]:border-b [&_tr]:border-border"
      />
    </div>
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
    table,
    Accordion,
    Accordions,
    Callout,
    Choice,
    Choices,
    Step,
    Screenshot,
    ScreenshotPlaceholder,
    Kbd,
    Diagram,
    DiagramPlaceholder,
    PlatformMatrix,
    TypeRow,
    TypeTable,
  };
}
