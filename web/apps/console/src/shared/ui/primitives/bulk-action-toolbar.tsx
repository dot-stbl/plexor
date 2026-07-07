import { Button } from '@/shared/ui/primitives/button';
import { MonoNum } from '@/shared/ui/primitives/mono-num';
import { cn } from '@/shared/lib/utils';

import type { ComponentProps, ReactNode } from 'react';

/**
 * BulkActionToolbar — appears above a table when rows are selected.
 *
 * Built on shadcn Button + Plexor DS MonoNum. Composed from shadcn
 * primitives, not a custom abstract primitive.
 *
 * Visual:
 *   ┌────────────────────────────────────────────────────────┐
 *   │  3 selected   [Suspend]  [Delete]      Clear selection  │
 *   └────────────────────────────────────────────────────────┘
 *
 * @example
 *   const [selected, setSelected] = useState<string[]>([]);
 *   <BulkActionToolbar
 *     count={selected.length}
 *     onClear={() => setSelected([])}
 *     actions={[
 *       { label: 'Suspend', onClick: handleSuspend, variant: 'outline' },
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
}

export function BulkActionToolbar({
  count,
  onClear,
  actions,
  entityLabel = 'selected',
  className,
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
        'sticky top-12 z-30 flex items-center gap-3 border-b border-border bg-card px-4 py-2 shadow-sm',
        className,
      )}
      {...props}
    >
      <span className="text-sm">
        <MonoNum>{count}</MonoNum>{' '}
        <span className="text-muted-foreground">{entityLabel}</span>
      </span>
      <div className="flex flex-1 items-center gap-1.5">
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
      <Button size="sm" variant="ghost" onClick={onClear}>
        Clear selection
      </Button>
    </div>
  );
}