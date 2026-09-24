import { useTranslation } from 'react-i18next';
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
 * Delegates to the shared `BulkActionToolbar` primitive (which localizes the
 * «N selected» label itself); action labels come from i18n `common.*`.
 */
export function VmBulkToolbar({
  count,
  onClear,
  onStart,
  onStop,
  onReboot,
  onDelete,
}: VmBulkToolbarProps) {
  const { t } = useTranslation();
  const actions: BulkActionAction[] = [
    { label: t('common.start'), icon: <PlayArrow />, onClick: onStart },
    { label: t('common.stop'), icon: <Stop />, onClick: onStop },
    { label: t('common.reboot'), icon: <Sync />, onClick: onReboot },
    { label: t('common.delete'), icon: <Delete />, variant: 'destructive', onClick: onDelete },
  ];

  return (
    <div data-od-id="vms-bulk">
      <BulkActionToolbar count={count} onClear={onClear} actions={actions} />
    </div>
  );
}
