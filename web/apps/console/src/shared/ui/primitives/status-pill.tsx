import { cn } from '@/shared/lib/utils';

import type { ComponentProps } from 'react';

/**
 * StatusPill — compact status indicator (dot + label).
 *
 * Abstract: used for any status (running/idle/error/pending/etc.) across
 * the app — table columns, badges, list items, etc.
 *
 * Variants:
 *   Core (4 colors): ok, err, warn, idle
 *   Sync aliases: running=ok, failed=err, pending=warn, stopped=idle
 *   New (semantic): info (cyan), archived (idle/gray), new (info/cyan),
 *                    beta (warn/yellow), deprecated (err/red), draft (idle/gray)
 *
 * Uses Plexor DS status semantics tokens (bg-ok-soft, text-ok-ink, etc.).
 */
export type StatusVariant =
  | 'ok'
  | 'err'
  | 'warn'
  | 'idle'
  | 'info'
  | 'running'
  | 'failed'
  | 'pending'
  | 'stopped'
  | 'archived'
  | 'new'
  | 'beta'
  | 'deprecated'
  | 'draft';

const SOFT_BG: Record<StatusVariant, string> = {
  ok: 'bg-ok-soft text-ok-ink',
  running: 'bg-ok-soft text-ok-ink',
  err: 'bg-err-soft text-err-ink',
  failed: 'bg-err-soft text-err-ink',
  warn: 'bg-warn-soft text-warn-ink',
  pending: 'bg-warn-soft text-warn-ink',
  info: 'bg-info-soft text-info-ink',
  idle: 'bg-idle-soft text-idle-ink',
  stopped: 'bg-idle-soft text-idle-ink',
  archived: 'bg-idle-soft text-idle-ink',
  new: 'bg-info-soft text-info-ink',
  beta: 'bg-warn-soft text-warn-ink',
  deprecated: 'bg-err-soft text-err-ink',
  draft: 'bg-idle-soft text-idle-ink',
};

const DOT_BG: Record<StatusVariant, string> = {
  ok: 'bg-ok',
  running: 'bg-ok',
  err: 'bg-err',
  failed: 'bg-err',
  warn: 'bg-warn',
  pending: 'bg-warn',
  info: 'bg-info',
  idle: 'bg-idle',
  stopped: 'bg-idle',
  archived: 'bg-idle',
  new: 'bg-info',
  beta: 'bg-warn',
  deprecated: 'bg-err',
  draft: 'bg-idle',
};

export interface StatusPillProps extends ComponentProps<'span'> {
  variant: StatusVariant;
  hideDot?: boolean;
  /** sm = h-5 (compact), md = h-6 (default, comfortable) */
  size?: 'sm' | 'md';
}

export function StatusPill({
  variant,
  hideDot = false,
  size = 'md',
  className,
  children,
  ...props
}: StatusPillProps) {
  return (
    <span
      data-slot="status-pill"
      data-size={size}
      data-variant={variant}
      className={cn(
        'inline-flex items-center gap-1.5 rounded-full font-medium whitespace-nowrap h-5 px-1.5 text-[10px] data-[size=md]:h-6 data-[size=md]:px-2 data-[size=md]:text-xs',
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
