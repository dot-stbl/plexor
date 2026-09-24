import type { ReactNode } from 'react';

/**
 * Step — numbered procedural marker for how-to recipes.
 *
 * Each step renders a numeric badge aligned to the first line of the
 * content block. The number is hard-set by the caller (not auto-counted)
 * because chapters occasionally need to skip a number ("Step 4 was
 * accepted by the cluster as one unit; we present 4a, 4b instead").
 *
 * The component is intentionally lightweight — no list chrome, no
 * connector lines — so a how-to page can interrupt the prose at any
 * point without the steps bleeding into the surrounding content.
 */
export interface StepProps {
  readonly number: number;
  readonly title: string;
  readonly children: ReactNode;
}

export function Step({ number, title, children }: StepProps): ReactNode {
  const badge = String(number).padStart(2, '0');
  return (
    <section className="docs-prose my-8 flex gap-4">
      <div className="flex shrink-0 flex-col items-center">
        <span
          aria-hidden="true"
          className="flex h-8 w-8 items-center justify-center rounded-full border border-border bg-surface-2 font-mono text-[11px] font-medium tracking-tight text-foreground"
        >
          {badge}
        </span>
      </div>
      <div className="min-w-0 flex-1">
        <h3 className="mb-2 mt-0 text-base font-semibold tracking-tight text-foreground">
          {title}
        </h3>
        <div className="[&>p]:my-2 [&>p:first-child]:mt-0 [&>p:last-child]:mb-0 [&>ul]:my-2 [&>ol]:my-2">
          {children}
        </div>
      </div>
    </section>
  );
}
