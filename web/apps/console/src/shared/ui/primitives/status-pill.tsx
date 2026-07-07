import { cn } from '@/lib/utils';

import type { ComponentProps } from 'react';

/**
 * StatusPill — compact status indicator (dot + label).
 *
 * Abstract: used for any status (running/idle/error/pending/etc.) across
 * the app — table columns, badges, list items, etc.
 *
 * Uses Plexor DS status semantics tokens (bg-ok-soft, text-ok-ink, etc.).
 */
export type StatusVariant =
  | 'ok'
  | 'err'
  | 'warn'
  | 'idle'
  | 'running'
  | 'failed'
  | 'pending'
  | 'stopped';

const SOFT_BG: Record<StatusVariant, string> = {
  ok: 'bg-ok-soft text-ok-ink',
  running: 'bg-ok-soft text-ok-ink',
  err: 'bg-err-soft text-err-ink',
  failed: 'bg-err-soft text-err-ink',
  warn: 'bg-warn-soft text-warn-ink',
  pending: 'bg-warn-soft text-warn-ink',
  idle: 'bg-idle-soft text-idle-ink',
  stopped: 'bg-idle-soft text-idle-ink',
};

const DOT_BG: Record<StatusVariant, string> = {
  ok: 'bg-ok',
  running: 'bg-ok',
  err: 'bg-err',
  failed: 'bg-err',
  warn: 'bg-warn',
  pending: 'bg-warn',
  idle: 'bg-idle',
  stopped: 'bg-idle',
};

export interface StatusPillProps extends ComponentProps<'span'> {
  variant: StatusVariant;
  hideDot?: boolean;
}

export function StatusPill({
  variant,
  hideDot = false,
  className,
  children,
  ...props
}: StatusPillProps) {
  return (
    <span
      data-slot="status-pill"
      data-variant={variant}
      className={cn(
        'inline-flex items-center gap-1.5 rounded-full px-2 py-0.5 text-xs font-medium whitespace-nowrap',
        SOFT_BG[variant],
        className,
      )}
      {...props}
    >
      {!hideDot && (
        <span
          aria-hidden
          data-slot="status-pill-dot"
          className={cn('size-1.5 rounded-full', DOT_BG[variant])}
        />
      )}
      {children}
    </span>
  );
}
