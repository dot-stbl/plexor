import { cn } from '@/shared/lib/utils';

import type { ComponentProps } from 'react';

/**
 * Toolbar — abstract generic panel primitive.
 *
 * Composable subcomponents for building settings panels, detail sidebars,
 * or any "list of sections with rows" pattern.
 *
 * Layout pattern:
 *   <Toolbar>                          // root: sectioned panel
 *     <ToolbarHeader>                   // optional header (title + actions)
 *       <ToolbarTitle>Display</ToolbarTitle>
 *       <ToolbarActions>...</ToolbarActions>
 *     </ToolbarHeader>
 *     <ToolbarContent>                  // scrollable body
 *       <ToolbarSection>                // optional section
 *         <ToolbarSectionLabel>Theme</ToolbarSectionLabel>
 *         <ToolbarItems>
 *           <ToolbarItem>               // row: label + control
 *             <span>Mode</span>
 *             <Switch />
 *           </ToolbarItem>
 *         </ToolbarItems>
 *       </ToolbarSection>
 *     </ToolbarContent>
 *   </Toolbar>
 *
 * Note: this is a settings/panel primitive, not a horizontal action bar.
 * For horizontal action bars (filter bar, bulk action bar), compose shadcn
 * `Button` + `InputGroup` directly.
 */
export function Toolbar({ className, ...props }: ComponentProps<'div'>) {
  return (
    <div
      data-slot="toolbar"
      className={cn(
        'flex flex-col divide-y divide-border rounded-lg border border-border bg-card text-sm',
        className,
      )}
      {...props}
    />
  );
}

/**
 * ToolbarHeader — top section of a Toolbar with title + right-aligned actions.
 */
export function ToolbarHeader({ className, ...props }: ComponentProps<'div'>) {
  return (
    <div
      data-slot="toolbar-header"
      className={cn('flex items-center justify-between gap-3 px-4 py-3', className)}
      {...props}
    />
  );
}

/**
 * ToolbarTitle — heading inside a ToolbarHeader.
 */
export function ToolbarTitle({ className, ...props }: ComponentProps<'h2'>) {
  return (
    <h2
      data-slot="toolbar-title"
      className={cn('text-base font-semibold tracking-tight', className)}
      {...props}
    />
  );
}

/**
 * ToolbarActions — right-aligned action group inside a ToolbarHeader.
 */
export function ToolbarActions({ className, ...props }: ComponentProps<'div'>) {
  return (
    <div
      data-slot="toolbar-actions"
      className={cn('flex items-center gap-1.5', className)}
      {...props}
    />
  );
}

/**
 * ToolbarContent — body of a Toolbar.
 */
export function ToolbarContent({ className, ...props }: ComponentProps<'div'>) {
  return (
    <div
      data-slot="toolbar-content"
      className={cn('flex flex-col', className)}
      {...props}
    />
  );
}

/**
 * ToolbarSection — grouped section within ToolbarContent.
 */
export function ToolbarSection({ className, ...props }: ComponentProps<'section'>) {
  return (
    <section
      data-slot="toolbar-section"
      className={cn('flex flex-col gap-2 px-4 py-3', className)}
      {...props}
    />
  );
}

/**
 * ToolbarSectionLabel — heading for a ToolbarSection.
 */
export function ToolbarSectionLabel({ className, ...props }: ComponentProps<'h3'>) {
  return (
    <h3
      data-slot="toolbar-section-label"
      className={cn(
        'text-[11px] uppercase tracking-[0.06em] text-muted-foreground font-medium',
        className,
      )}
      {...props}
    />
  );
}

/**
 * ToolbarItems — list of items within a section.
 */
export function ToolbarItems({ className, ...props }: ComponentProps<'div'>) {
  return (
    <div
      data-slot="toolbar-items"
      className={cn('flex flex-col gap-3', className)}
      {...props}
    />
  );
}

/**
 * ToolbarItem — single row in a settings panel.
 * Renders label on the left and control on the right by default.
 */
export function ToolbarItem({ className, ...props }: ComponentProps<'div'>) {
  return (
    <div
      data-slot="toolbar-item"
      className={cn('flex items-center justify-between gap-3', className)}
      {...props}
    />
  );
}

/**
 * ToolbarLabel — label text on the left side of a ToolbarItem.
 */
export function ToolbarLabel({ className, ...props }: ComponentProps<'label'>) {
  return (
    <label
      data-slot="toolbar-label"
      className={cn('text-foreground font-medium', className)}
      {...props}
    />
  );
}

/**
 * ToolbarDescription — secondary text under a ToolbarItem label.
 */
export function ToolbarDescription({ className, ...props }: ComponentProps<'p'>) {
  return (
    <p
      data-slot="toolbar-description"
      className={cn('text-muted-foreground text-xs', className)}
      {...props}
    />
  );
}

/**
 * ToolbarSeparator — thin divider between items.
 */
export function ToolbarSeparator({ className, ...props }: ComponentProps<'div'>) {
  return (
    <div
      data-slot="toolbar-separator"
      role="separator"
      className={cn('h-px bg-border', className)}
      {...props}
    />
  );
}

/**
 * ToolbarGroup — horizontal cluster of items.
 */
export function ToolbarGroup({ className, ...props }: ComponentProps<'div'>) {
  return (
    <div
      data-slot="toolbar-group"
      className={cn('flex items-center gap-2', className)}
      {...props}
    />
  );
}

/**
 * ToolbarEmpty — placeholder for empty state.
 */
export function ToolbarEmpty({ className, ...props }: ComponentProps<'div'>) {
  return (
    <div
      data-slot="toolbar-empty"
      className={cn('text-muted-foreground py-8 text-center text-sm', className)}
      {...props}
    />
  );
}