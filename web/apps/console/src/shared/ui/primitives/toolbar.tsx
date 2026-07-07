import { cn } from '@/shared/lib/utils';

import type { ComponentProps } from 'react';

/**
 * Toolbar — filter bar primitive for tables and lists.
 *
 * Abstract: any "search + filter chips + actions" surface
 * (table toolbars, page filters, search headers).
 *
 * Layout:
 *   [search input]  |  [filter chips]  |  [actions / menu]
 *
 * shadcn-ui's @shadcn registry has no Toolbar primitive, so we provide it.
 */
export function Toolbar({ className, ...props }: ComponentProps<'div'>) {
  return (
    <div
      data-slot="toolbar"
      className={cn(
        'flex flex-wrap items-center gap-2 border-b border-border bg-card px-3 py-2',
        className,
      )}
      {...props}
    />
  );
}

/**
 * ToolbarSearch — left-aligned search input with icon addon.
 * Use as the first child of <Toolbar>.
 */
export function ToolbarSearch({
  className,
  ...props
}: ComponentProps<'div'>) {
  return (
    <div
      data-slot="toolbar-search"
      className={cn('flex-1 min-w-48 max-w-72', className)}
      {...props}
    />
  );
}

/**
 * ToolbarFilter — group of filter chips (status, region, etc.) with
 * shared visual separator. Use between search and actions.
 */
export function ToolbarFilter({ className, ...props }: ComponentProps<'div'>) {
  return (
    <div
      data-slot="toolbar-filter"
      className={cn(
        'flex items-center gap-1 border-l border-r border-border px-2 first:border-l-0 first:pl-0 last:border-r-0 last:pr-0',
        className,
      )}
      {...props}
    />
  );
}

/**
 * ToolbarSeparator — thin vertical divider between toolbar groups.
 */
export function ToolbarSeparator({ className, ...props }: ComponentProps<'div'>) {
  return (
    <div
      data-slot="toolbar-separator"
      role="separator"
      className={cn('h-5 w-px bg-border', className)}
      {...props}
    />
  );
}

/**
 * ToolbarActions — right-aligned action group.
 */
export function ToolbarActions({ className, ...props }: ComponentProps<'div'>) {
  return (
    <div
      data-slot="toolbar-actions"
      className={cn('flex items-center gap-1.5 ml-auto', className)}
      {...props}
    />
  );
}
