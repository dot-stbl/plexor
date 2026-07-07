import { cn } from '@/lib/utils';

import type { ComponentProps } from 'react';

/**
 * Toolbar — filter bar with grouped controls and end-aligned actions.
 *
 * Abstract: any "filter chips + search + actions" surface
 * (table toolbars, page filters, search headers).
 *
 * shadcn-ui's @shadcn registry doesn't have a Toolbar primitive, so
 * we provide it as a Plexor DS custom primitive.
 */
export function Toolbar({ className, ...props }: ComponentProps<'div'>) {
  return (
    <div
      data-slot="toolbar"
      className={cn(
        'flex flex-wrap items-center gap-3 border-b border-border bg-card px-3 py-2',
        className,
      )}
      {...props}
    />
  );
}

export function ToolbarGroup({ className, ...props }: ComponentProps<'div'>) {
  return (
    <div
      data-slot="toolbar-group"
      className={cn(
        'flex items-center gap-1 border-r border-border pr-3 last:border-r-0 last:pr-0',
        className,
      )}
      {...props}
    />
  );
}

export function ToolbarEnd({ className, ...props }: ComponentProps<'div'>) {
  return (
    <div
      data-slot="toolbar-end"
      className={cn('flex items-center gap-2 ml-auto', className)}
      {...props}
    />
  );
}

export function ToolbarSeparator({ className, ...props }: ComponentProps<'div'>) {
  return (
    <div
      data-slot="toolbar-separator"
      className={cn('h-5 w-px bg-border', className)}
      role="separator"
      {...props}
    />
  );
}
