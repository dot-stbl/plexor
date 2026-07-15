import { useCallback, useMemo, useState } from 'react';

/**
 * Row-selection state for a `DataTable` — the checkbox feature. Selection lives
 * in a `Set<string>` (O(1) membership). Returns the ready-made `selection` prop
 * object for `<DataTable selection={...}>` plus `clear` for the bulk bar.
 *
 * Pass the CURRENTLY VISIBLE rows (e.g. the filtered list) so «select all»
 * targets what the user sees, matching the header checkbox.
 *
 * @example
 *   const sel = useRowSelection(filtered);
 *   <DataTable ... selection={sel.selection} />
 *   <BulkActionToolbar count={sel.selectedIds.size} onClear={sel.clear} actions={[…]} />
 */
export interface RowSelection {
  selectedIds: Set<string>;
  toggle: (id: string) => void;
  toggleAll: (next: boolean) => void;
  clear: () => void;
  selection: {
    selectedIds: ReadonlySet<string>;
    onToggle: (id: string) => void;
    onToggleAll: (next: boolean) => void;
  };
}

export function useRowSelection<T extends { id: string }>(rows: T[]): RowSelection {
  const [selectedIds, setSelectedIds] = useState<Set<string>>(() => new Set());

  const toggle = useCallback((id: string) => {
    setSelectedIds((prev) => {
      const next = new Set(prev);
      if (next.has(id)) next.delete(id);
      else next.add(id);
      return next;
    });
  }, []);

  const toggleAll = useCallback(
    (next: boolean) => setSelectedIds(next ? new Set(rows.map((r) => r.id)) : new Set()),
    [rows],
  );

  const clear = useCallback(() => setSelectedIds(new Set()), []);

  const selection = useMemo(
    () => ({ selectedIds, onToggle: toggle, onToggleAll: toggleAll }),
    [selectedIds, toggle, toggleAll],
  );

  return { selectedIds, toggle, toggleAll, clear, selection };
}
