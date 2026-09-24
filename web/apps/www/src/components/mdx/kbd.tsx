import type { ReactNode } from 'react';

/**
 * Kbd — keyboard-shortcut hint rendered inline with other prose.
 *
 * Renders a small monospace pill. Multiple `<Kbd>` separated by `+`
 * chain together visually — the operator reads `Ctrl + K` instead
 * of three inline pills with no relation.
 *
 * The `aria-keyshortcuts` attribute is set so screen readers
 * announce the shortcut as a unit.
 */
export interface KbdProps {
  readonly children: ReactNode;
  readonly shortcut?: string;
}

export function Kbd({ children, shortcut }: KbdProps): ReactNode {
  return (
    <kbd
      className="mx-0.5 inline-flex items-center rounded border border-border bg-surface-2 px-1.5 py-0.5 font-mono text-[0.8125em] leading-none text-foreground shadow-[inset_0_-1px_0_var(--border)]"
      aria-keyshortcuts={typeof shortcut === 'string' ? shortcut : undefined}
    >
      {children}
    </kbd>
  );
}
