import type { ReactNode } from 'react';
import { StatusPill, type RoadmapStatus } from './status-pill';

/**
 * Phase item — a single row of the roadmap timeline. The card layout
 * is one monospace version chip + title + a status pill, then a two-
 * column body (Shipped / Planned). Empty sections hide their column.
 *
 * Bullets are spaced tight — operators come here to scan, not to
 * read — so item labels are short noun phrases, not full sentences.
 */

export interface RoadmapPhase {
  readonly id: string;
  readonly version: string;
  readonly title: string;
  readonly status: RoadmapStatus;
  readonly shipped: readonly string[];
  readonly planned: readonly string[];
}

export function PhaseItem({
  phase,
  shippedLabel,
  plannedLabel,
}: {
  phase: RoadmapPhase;
  shippedLabel: string;
  plannedLabel: string;
}): ReactNode {
  const hasShipped = phase.shipped.length > 0;
  const hasPlanned = phase.planned.length > 0;

  return (
    <li className="rounded-xl border border-border bg-card">
      <div className="flex flex-wrap items-baseline justify-between gap-3 border-b border-border/60 p-5">
        <div className="flex items-baseline gap-3">
          <code className="rounded bg-muted px-1.5 py-0.5 font-mono text-xs text-foreground">
            {phase.version}
          </code>
          <h3 className="m-0 text-lg font-semibold text-foreground">
            {phase.title}
          </h3>
        </div>
        <StatusPill status={phase.status} />
      </div>

      {(hasShipped || hasPlanned) && (
        <div className="grid grid-cols-1 gap-4 p-5 text-sm md:grid-cols-2">
          {hasShipped && (
            <BulletList
              items={phase.shipped}
              label={shippedLabel}
              mark="✓"
              markClass="text-ok"
            />
          )}
          {hasPlanned && (
            <BulletList
              items={phase.planned}
              label={plannedLabel}
              mark="·"
              markClass="text-muted-2"
            />
          )}
        </div>
      )}
    </li>
  );
}

function BulletList({
  items,
  label,
  mark,
  markClass,
}: {
  items: readonly string[];
  label: string;
  mark: string;
  markClass: string;
}): ReactNode {
  return (
    <div>
      <div className="mb-1.5 font-mono text-[0.7rem] font-medium uppercase tracking-[0.18em] text-muted-2">
        {label}
      </div>
      <ul className="space-y-1.5">
        {items.map((item) => (
          <li key={item} className="flex items-start gap-2">
            <span className={`mt-0.5 inline-flex font-medium ${markClass}`}>
              {mark}
            </span>
            <span className="text-foreground">{item}</span>
          </li>
        ))}
      </ul>
    </div>
  );
}