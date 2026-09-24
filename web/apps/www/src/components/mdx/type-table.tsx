'use client';

import type { ReactNode } from 'react';

/**
 * TypeTable — per-row API reference table for endpoints, permission
 * catalog entries, and capability matrices.
 *
 * Each row carries five slots: `name`, `type`, `required` (boolean
 * pill or em-dash), `default` (monospace or em-dash), and
 * `description` (arbitrary MDX — inline `<code>`, links, even a
 * nested `<Callout>`). The caller chooses which slots to populate;
 * the row handles the layout.
 *
 * On small screens the row stacks (name on top, then a secondary
 * line with type + required + default, then description below). On
 * `md:` and up it sits in a five-column real `<table>` — same shape
 * as the API reference tables elsewhere in the docs.
 *
 * Stays inside the Plexor DS — no new tokens, no bespoke colours.
 * Borders and muted prose match `Callout` / `Step` / `PlatformMatrix`
 * so the page reads as one typographic surface.
 */
export interface TypeRowProps {
  readonly name: string;
  readonly type: string;
  readonly required?: boolean;
  readonly default?: string;
  readonly description: ReactNode;
}

function TypePill({ type }: { readonly type: string }): ReactNode {
  return (
    <span className="inline-flex items-center rounded bg-surface-3 px-1.5 py-0.5 font-mono text-[0.6875rem] text-muted-foreground">
      {type}
    </span>
  );
}

function RequiredPill(): ReactNode {
  return (
    <span className="inline-flex items-center rounded bg-err/10 px-1.5 py-0.5 font-mono text-[0.6875rem] uppercase tracking-[0.14em] text-err-ink">
      required
    </span>
  );
}

export function TypeRow({
  name,
  type,
  required = false,
  default: defaultValue,
  description,
}: TypeRowProps): ReactNode {
  return (
    <div className="grid grid-cols-1 gap-y-2 border-b border-border py-4 last:border-b-0 md:grid-cols-[minmax(8rem,1fr)_minmax(6rem,auto)_minmax(6rem,auto)_minmax(6rem,auto)_minmax(0,2fr)] md:gap-x-4 md:py-3">
      <div className="flex items-center md:items-start">
        <code className="rounded border border-border bg-surface-2 px-1.5 py-0.5 font-mono text-[0.8125rem] text-foreground">
          {name}
        </code>
      </div>
      <div className="flex items-center md:items-start">
        <TypePill type={type} />
      </div>
      <div className="flex items-center md:items-start">
        {required ? (
          <RequiredPill />
        ) : (
          <span className="font-mono text-xs text-muted-2">—</span>
        )}
      </div>
      <div className="flex items-center md:items-start">
        {defaultValue !== undefined ? (
          <code className="font-mono text-xs text-foreground">{defaultValue}</code>
        ) : (
          <span className="font-mono text-xs text-muted-2">—</span>
        )}
      </div>
      <div className="text-md leading-6 text-muted-foreground [&_code]:font-mono [&_code]:text-[0.8125em] [&_p]:my-1 [&_p:first-child]:mt-0 [&_p:last-child]:mb-0 [&_ul]:my-1 [&_ol]:my-1">
        {description}
      </div>
    </div>
  );
}

export interface TypeTableProps {
  readonly children: ReactNode;
}

/**
 * TypeTable — container for `<TypeRow>` children. The header legend
 * (Name / Type / Required / Default / Description) renders on `md:`;
 * on mobile the column layout collapses to a stacked card per row.
 *
 * Each `<TypeRow>` is a `<div>` rather than `<tr>` so the layout
 * switches between flex-stack and grid cleanly without forcing a
 * single `<table>` rendering path. The container is also a flex
 * element so a `<TypeTable>` can sit anywhere in the article
 * column without breaking the prose flow.
 */
export function TypeTable({ children }: TypeTableProps): ReactNode {
  return (
    <figure className="docs-prose my-6">
      <div className="overflow-x-auto rounded-lg border border-border bg-card px-4 md:px-5">
        <div className="hidden border-b border-border-2 pb-2 md:grid md:grid-cols-[minmax(8rem,1fr)_minmax(6rem,auto)_minmax(6rem,auto)_minmax(6rem,auto)_minmax(0,2fr)] md:gap-x-4">
          <span className="font-mono text-[10px] font-medium uppercase tracking-[0.14em] text-muted-2">
            Name
          </span>
          <span className="font-mono text-[10px] font-medium uppercase tracking-[0.14em] text-muted-2">
            Type
          </span>
          <span className="font-mono text-[10px] font-medium uppercase tracking-[0.14em] text-muted-2">
            Required
          </span>
          <span className="font-mono text-[10px] font-medium uppercase tracking-[0.14em] text-muted-2">
            Default
          </span>
          <span className="font-mono text-[10px] font-medium uppercase tracking-[0.14em] text-muted-2">
            Description
          </span>
        </div>
        {children}
      </div>
    </figure>
  );
}
