import type { ReactNode } from 'react';

/**
 * Callout — themed block for info / warning / tip / note / danger.
 *
 * Lives next to the docs site that consumes it (the CONTENT-PLAN keeps
 * docs deliberately decoupled from `@plexor/ui`). Reads Plexor DS
 * tokens (`bg-surface-2`, `text-fg-2`, `border-border-2`, status
 * colours) so the callout blends with the active preset without
 * hand-picked hex values.
 *
 * The `title` prop is optional — when omitted, the component falls back
 * to the `type` itself, capitalised.
 */
export type CalloutType = 'info' | 'warning' | 'tip' | 'note' | 'danger';

export interface CalloutProps {
  readonly type?: CalloutType;
  readonly title?: string;
  readonly children: ReactNode;
}

const TYPE_STYLES: Record<CalloutType, string> = {
  info: 'border-info/40 bg-info/10 text-foreground [&_a]:text-info',
  warning: 'border-warn/40 bg-warn/10 text-foreground [&_a]:text-warn-ink',
  tip: 'border-ok/40 bg-ok/10 text-foreground [&_a]:text-ok-ink',
  note: 'border-border-2 bg-surface-2 text-foreground',
  danger: 'border-err/50 bg-err/10 text-foreground [&_a]:text-err-ink',
};

const TYPE_LABELS: Record<CalloutType, string> = {
  info: 'Info',
  warning: 'Warning',
  tip: 'Tip',
  note: 'Note',
  danger: 'Danger',
};

export function Callout({ type = 'note', title, children }: CalloutProps): ReactNode {
  const heading = title ?? TYPE_LABELS[type];
  return (
    <aside
      className={`my-6 rounded-lg border-l-2 px-4 py-3 text-sm leading-6 ${TYPE_STYLES[type]}`}
      role={type === 'danger' || type === 'warning' ? 'note' : undefined}
    >
      <div className="mb-1 font-mono text-[10px] font-medium uppercase tracking-[0.14em] text-muted-2">
        {heading}
      </div>
      <div className="[&>p]:my-1 [&>p:first-child]:mt-0 [&>p:last-child]:mb-0 [&>ul]:my-1 [&>ol]:my-1">
        {children}
      </div>
    </aside>
  );
}
