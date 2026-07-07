import type { ReactNode } from 'react';
import { cn } from '@/lib/utils';

/**
 * Status pill — solid dot + label, density for tables.
 *
 * Variants map to Plexor DS status semantics tokens:
 *   ok / running   → --ok-soft + --ok-ink
 *   err / failed   → --err-soft + --err-ink
 *   warn / pending → --warn-soft + --warn-ink
 *   idle / stopped → --idle-soft + --idle-ink
 *
 * Used in tables (status column), badges in detail views, etc.
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

const VARIANT_CLASSES: Record<StatusVariant, string> = {
  ok: 'bg-ok-soft text-ok-ink',
  running: 'bg-ok-soft text-ok-ink',
  err: 'bg-err-soft text-err-ink',
  failed: 'bg-err-soft text-err-ink',
  warn: 'bg-warn-soft text-warn-ink',
  pending: 'bg-warn-soft text-warn-ink',
  idle: 'bg-idle-soft text-idle-ink',
  stopped: 'bg-idle-soft text-idle-ink',
};

const DOT_CLASSES: Record<StatusVariant, string> = {
  ok: 'bg-ok',
  running: 'bg-ok',
  err: 'bg-err',
  failed: 'bg-err',
  warn: 'bg-warn',
  pending: 'bg-warn',
  idle: 'bg-idle',
  stopped: 'bg-idle',
};

export interface StatusPillProps {
  /** Variants: 'ok' | 'err' | 'warn' | 'idle' or semantic aliases 'running'/'failed'/'pending'/'stopped'. */
  variant: StatusVariant;
  /** Label shown next to the dot. */
  children: ReactNode;
  /** Hide the dot — labels-only mode. */
  hideDot?: boolean;
  className?: string;
}

export function StatusPill({ variant, children, hideDot = false, className }: StatusPillProps) {
  return (
    <span
      className={cn(
        'inline-flex items-center gap-1.5 rounded-sm px-1.5 py-0.5 text-xs font-medium',
        VARIANT_CLASSES[variant],
        className,
      )}
    >
      {!hideDot && (
        <span
          aria-hidden
          className={cn('size-1.5 rounded-full', DOT_CLASSES[variant])}
        />
      )}
      {children}
    </span>
  );
}
