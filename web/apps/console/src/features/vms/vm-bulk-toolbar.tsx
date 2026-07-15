import { BulkActionToolbar, type BulkActionAction } from '@/shared/ui/primitives/bulk-action-toolbar';
import { Delete, PlayArrow, Stop, Sync } from '@nine-thirty-five/material-symbols-react/rounded/700';

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
    { label: 'Запустить', icon: <PlayArrow />, onClick: onStart },
    { label: 'Остановить', icon: <Stop />, onClick: onStop },
    { label: 'Перезагрузить', icon: <Sync />, onClick: onReboot },
    {
      label: 'Удалить',
      icon: <Delete />,
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