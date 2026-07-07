import { cn } from '@/lib/utils';

import type { ComponentProps } from 'react';

/**
 * MonoNum — span with Plexor DS mono font + tabular numerals.
 *
 * Abstract: used for any numeric display (IPs, IDs, sizes, durations,
 * money, timestamps) where alignment and readability matter.
 */
export interface MonoNumProps extends ComponentProps<'span'> {
  /** Use `text-muted-foreground` for secondary values. */
  muted?: boolean;
}

export function MonoNum({ muted = false, className, children, ...props }: MonoNumProps) {
  return (
    <span
      data-slot="mono-num"
      data-muted={muted || undefined}
      className={cn(
        'inline-block align-middle font-mono tracking-tight tabular-nums leading-none',
        muted && 'text-muted-foreground',
        className,
      )}
      {...props}
    >
      {children}
    </span>
  );
}
