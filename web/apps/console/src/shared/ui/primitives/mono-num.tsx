import type { ReactNode } from 'react';
import { cn } from '@/lib/utils';

/**
 * MonoNum — span with Plexor DS mono + tabular numerals.
 *
 * For any numeric column: IPs, IDs, byte sizes, durations, currency.
 * Renders inline; semantically a span so it doesn't break layouts.
 *
 * Size variants match Plexor DS row sizing — tabular numerals
 * align decimals in dense columns.
 */
export interface MonoNumProps {
  children: ReactNode;
  /** Use `muted-foreground` color (for secondary numbers). */
  muted?: boolean;
  /** Apply `tabular-nums` (default true). */
  tabular?: boolean;
  className?: string;
}

export function MonoNum({ children, muted = false, tabular = true, className }: MonoNumProps) {
  return (
    <span
      className={cn(
        'font-mono tracking-tight',
        tabular && 'tabular-nums',
        muted && 'text-muted-foreground',
        className,
      )}
    >
      {children}
    </span>
  );
}
