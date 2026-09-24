import type { ReactNode } from 'react';

/**
 * Status pill — shared by every roadmap row. Three states (shipped /
 * next / design) with distinct OK / WARN / WARN-soft colours. The
 * "next" and "design" states share WARN tokens because both are
 * non-shipped; the pill alone shouldn't claim more commitment than the
 * row's data does.
 *
 * The "done" state uses OK tokens — green-ink on green-soft is the
 * only moment the surface carries colour, by design: every other row
 * is greyscale, the ship milestone is the visual punctuation.
 */

export type RoadmapStatus = 'shipped' | 'next' | 'design';

const STATUS_LABEL: Readonly<Record<RoadmapStatus, string>> = {
  shipped: 'Готово',
  next: 'В работе',
  design: 'В дизайне',
};

const STATUS_CLASSES: Readonly<Record<RoadmapStatus, string>> = {
  shipped: 'border-ok/30 bg-ok-soft text-ok-ink',
  next: 'border-warn/30 bg-warn-soft text-warn-ink',
  design: 'border-warn/30 bg-warn-soft text-warn-ink',
};

export function StatusPill({ status }: { status: RoadmapStatus }): ReactNode {
  return (
    <span
      className={`shrink-0 rounded-full border px-2 py-0.5 font-mono text-[0.7rem] font-medium uppercase tracking-wider ${STATUS_CLASSES[status]}`}
    >
      {STATUS_LABEL[status]}
    </span>
  );
}