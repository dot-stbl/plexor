import { Button } from '@/shared/ui/primitives/button';
import { MonoNum } from '@/shared/ui/primitives/mono-num';
import { cn } from '@/shared/lib/utils';

import type { ComponentProps, ReactNode } from 'react';

/**
 * BulkActionToolbar — floating bottom panel for bulk actions.
 *
 * Appears when rows are selected in a list or table. Floats above the
 * page bottom (doesn't shift layout, doesn't compete with sticky header).
 *
 * Visual:
 *
 *    ┌────────────────────────────────────────────────────────┐
 *    │  3 selected   Suspend  Restart  Delete      Clear all  │
 *    └────────────────────────────────────────────────────────┘
 *
 * Slides in from below on appear, slides out on clear.
 * Built on shadcn Button + Plexor MonoNum. Not a custom abstract
 * primitive — composed from shadcn.
 *
 * @example
 *   const [selected, setSelected] = useState<string[]>([]);
 *   <BulkActionToolbar
 *     count={selected.length}
 *     onClear={() => setSelected([])}
 *     actions={[
 *       { label: 'Delete', onClick: handleDelete, variant: 'destructive' },
 *     ]}
 *   />
 */
export interface BulkActionAction {
  label: string;
  onClick: () => void;
  variant?: 'default' | 'outline' | 'ghost' | 'destructive' | 'secondary';
  disabled?: boolean;
  icon?: ReactNode;
}

export interface BulkActionToolbarProps extends Omit<ComponentProps<'div'>, 'children'> {
  count: number;
  onClear: () => void;
  actions: BulkActionAction[];
  entityLabel?: string;
  /** Optional custom position from bottom (in Tailwind spacing units). */
  bottomClass?: string;
}

export function BulkActionToolbar({
  count,
  onClear,
  actions,
  entityLabel = 'selected',
  className,
  bottomClass = 'bottom-4',
  ...props
}: BulkActionToolbarProps) {
  if (count === 0) return null;

  return (
    <div
      data-slot="bulk-action-toolbar"
      data-count={count}
      role="region"
      aria-label={`${count} ${entityLabel}`}
      className={cn(
        'fixed left-1/2 z-50 flex -translate-x-1/2 items-center gap-3 rounded-lg border border-border bg-card px-4 py-1.5 shadow-lg',
        'animate-in slide-in-from-bottom-4 fade-in duration-200',
        bottomClass,
        className,
      )}
      {...props}
    >
      <span className="text-sm font-medium">
        <MonoNum>{count}</MonoNum>{' '}
        <span className="text-muted-foreground">{entityLabel}</span>
      </span>
      <span className="self-stretch w-px bg-border" aria-hidden />
      <div className="flex items-center gap-1.5">
        {actions.map((action) => (
          <Button
            key={action.label}
            size="sm"
            variant={action.variant ?? 'outline'}
            onClick={action.onClick}
            disabled={action.disabled}
          >
            {action.icon}
            {action.label}
          </Button>
        ))}
      </div>
      <span className="self-stretch w-px bg-border" aria-hidden />
      <Button size="sm" variant="ghost" onClick={onClear}>
        Clear
      </Button>
    </div>
  );
}