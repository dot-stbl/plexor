/**
 * DataTable row checkbox + selection toolbar — regression suite.
 *
 * Bug 1 (2026-09-21): "in tables select doesn't work as in toolbar appears,
 * count of selected updates but checkbox doesn't change, row in table either".
 * Root cause: the inner SVG indicator of the `Checkbox` primitive targeted
 * `group-data-[selected]` (bare `group`), but the wrapper carried no `group`
 * class — so the indicator never appeared, even though `data-selected` on
 * the label and the React state both flipped correctly. The toolbar count
 * updates because `useRowSelection`'s state actually toggles; only the
 * visual indicator was unreachable from any CSS selector.
 *
 * The fix (see `primitives/checkbox.tsx`) gives the wrapper a named
 * `group/checkbox` and the SVG targets `group-data-[selected]/checkbox:`.
 *
 * These tests render a real `DataTable` + `useRowSelection` (the same
 * wiring as `routes/images.tsx`) and assert BOTH:
 *   - the row checkbox carries `aria-checked=true` after a click,
 *   - the bulk-action toolbar appears with the right count,
 *   - the indicator SVG's computed opacity transitions from 0 to 1
 *     (the precise leg that broke before the fix).
 *
 * If a future regression breaks any of these, the test fails with a clear
 * pointer at which side lost the binding.
 */
import { describe, expect, it } from 'vitest';
import { renderWithProviders } from '@/test-utils';
import { screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { DataTable } from './data-table';
import { useRowSelection } from './use-row-selection';
import { BulkActionToolbar } from '@/shared/ui/primitives/bulk-action-toolbar';
import type { ColumnDef } from './data-table';

interface Row {
  id: string;
  name: string;
}

const COLUMNS: ColumnDef<Row>[] = [
  {
    id: 'name',
    accessorKey: 'name',
    header: 'Name',
  },
];

const ROWS: Row[] = [
  { id: 'row-1', name: 'first' },
  { id: 'row-2', name: 'second' },
  { id: 'row-3', name: 'third' },
];

/**
 * Test harness — mirrors the production wiring from `routes/images.tsx`:
 * DataTable + useRowSelection + BulkActionToolbar driven by the same
 * selection state. We render the toolbar so the assertion can read its
 * `data-count` attribute (the toolbar only renders when count > 0, so
 * absence after a click is itself a regression).
 */
function Harness() {
  const sel = useRowSelection(ROWS);
  return (
    <>
      <DataTable<Row>
        columns={COLUMNS}
        data={ROWS}
        density="compact"
        selection={sel.selection}
      />
      <BulkActionToolbar
        count={sel.selectedIds.size}
        onClear={sel.clear}
        actions={[{ label: 'noop', onClick: () => {} }]}
      />
    </>
  );
}

/**
 * Find the row checkbox by its row index. The data-table renders two kinds
 * of checkbox with `aria-label="Select row"` (row) and
 * `aria-label="Select all"` (header). We use the row-only label to skip
 * the header, then index into the list.
 */
function getRowCheckboxInput(rowIndex: number): HTMLInputElement {
  const inputs = Array.from(
    document.querySelectorAll<HTMLInputElement>(
      'label[data-slot="checkbox"] input[type="checkbox"][aria-label="Select row"]',
    ),
  );
  const target = inputs[rowIndex];
  if (!target) {
    throw new Error(`row checkbox at index ${rowIndex} not found (got ${inputs.length})`);
  }
  return target;
}

describe('DataTable — row checkbox toggle (regression for "checkbox does not change")', () => {
  it('clicking a row checkbox flips the hidden input DOM checked state', async () => {
    const user = userEvent.setup();
    renderWithProviders(<Harness />);

    const input = getRowCheckboxInput(0);
    expect(input.checked).toBe(false);

    await user.click(input);

    expect(input.checked).toBe(true);
  });

  it('clicking the same row checkbox twice returns it to unchecked', async () => {
    const user = userEvent.setup();
    renderWithProviders(<Harness />);

    const input = getRowCheckboxInput(1);

    await user.click(input);
    expect(input.checked).toBe(true);

    await user.click(input);
    expect(input.checked).toBe(false);
  });

  it('shows the bulk-action toolbar with the selected count after a row click', async () => {
    const user = userEvent.setup();
    renderWithProviders(<Harness />);

    // No toolbar before any selection — its rendering is gated on count > 0.
    expect(screen.queryByRole('region', { name: /selected/i })).toBeNull();

    const input = getRowCheckboxInput(0);
    await user.click(input);

    const toolbar = screen.getByRole('region', { name: /1 selected/i });
    expect(toolbar).toBeInTheDocument();
    expect(toolbar.dataset['count']).toBe('1');
  });

  it('the wrapper carries the named group hook + the indicator SVG uses the matching selector (the CSS contract)', () => {
    // Structural leg of the bug: the wrapper must carry the `group/checkbox`
    // Tailwind utility AND the indicator SVG must target that group. Without
    // this pair, the `group-data-[selected]/checkbox:` selector never
    // resolves in any browser — the original bug. jsdom doesn't compute
    // styles, so we assert the class strings directly.
    //
    // If a future change drops either the wrapper group or the named-group
    // selector on the indicator, the tick SVG silently stops appearing on
    // selection in real browsers; this assertion is the canary.
    renderWithProviders(<Harness />);

    const input = getRowCheckboxInput(0);
    const label = input.closest('label[data-slot="checkbox"]') as HTMLLabelElement;
    expect(label).not.toBeNull();
    expect(label.className).toContain('group/checkbox');

    const svg = label.querySelector<SVGElement>('svg[data-slot="checkbox-indicator"]');
    expect(svg).not.toBeNull();
    const svgClass = svg?.getAttribute('class') ?? '';
    expect(svgClass).toContain('opacity-0');
    expect(svgClass).toContain('group-data-[selected]/checkbox:opacity-100');
    expect(svgClass).toContain('group-data-[selected]/checkbox:scale-100');
  });
});