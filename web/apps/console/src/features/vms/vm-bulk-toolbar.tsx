import { BulkActionToolbar, type BulkActionAction } from '@/shared/ui/primitives/bulk-action-toolbar';
import { Play, Stop, ArrowsClockwise, Trash } from '@phosphor-icons/react';

interface VmBulkToolbarProps {
  count: number;
  onClear: () => void;
  onStart: () => void;
  onStop: () => void;
  onReboot: () => void;
  onDelete: () => void;
}

/**
 * Floating bulk-action bar that surfaces when 1+ rows are selected.
 * Delegates to the shadcn `BulkActionToolbar` primitive so we keep the
 * floating-bottom + slide-in animation in one place.
 */
export function VmBulkToolbar({
  count,
  onClear,
  onStart,
  onStop,
  onReboot,
  onDelete,
}: VmBulkToolbarProps) {
  const actions: BulkActionAction[] = [
    { label: 'Запустить', icon: <Play />, onClick: onStart },
    { label: 'Остановить', icon: <Stop />, onClick: onStop },
    { label: 'Перезагрузить', icon: <ArrowsClockwise />, onClick: onReboot },
    {
      label: 'Удалить',
      icon: <Trash />,
      variant: 'destructive',
      onClick: onDelete,
    },
  ];

  return (
    <div data-od-id="vms-bulk">
      <BulkActionToolbar
        count={count}
        onClear={onClear}
        actions={actions}
        entityLabel="выбрано"
      />
    </div>
  );
}