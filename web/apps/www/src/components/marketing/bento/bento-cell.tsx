import type { ReactNode } from 'react';
import { Link } from '@tanstack/react-router';
import { StatusPill } from '@/components/ui/status-pill';
import { bentoStatusLabel, bentoStatusVariant, type BentoCellData } from './bento-data';
import {
  VisualCapacityBar,
  VisualChipRow,
  VisualCommandLine,
  VisualLogList,
  VisualScopePath,
  VisualStatusList,
} from './bento-visuals';

const VISUALS: Readonly<Record<BentoCellData['visual'], () => ReactNode>> = {
  'status-list': () => <VisualStatusList />,
  'chip-row': () => <VisualChipRow />,
  'capacity-bar': () => <VisualCapacityBar />,
  'scope-path': () => <VisualScopePath />,
  'log-list': () => <VisualLogList />,
  'command-line': () => <VisualCommandLine />,
};

/** One bento cell — icon, shipped/next pill, title, body, and a small decorative visual (spec 3.4 / amendment A3). */
export function BentoCell({ cell }: { cell: BentoCellData }) {
  const CellIcon = cell.icon;
  const visual = VISUALS[cell.visual];
  // Explicit `: string` so the template literal widens to the permissive
  // fallback on TanStack's `<Link to>` route union (see
  // docs-breadcrumb.tsx `crumb.to?: string` precedent) — a narrow
  // template-literal type wouldn't satisfy the union and `tsc` would
  // fail with TS2322.
  const cellHref: string = `/services/${cell.id}/`;

  return (
    <Link
      to={cellHref}
      className="block h-full rounded-2xl bg-card p-6 transition-colors hover:bg-muted"
    >
      <div className="flex items-start justify-between gap-2">
        <CellIcon className="size-5 text-muted-foreground" aria-hidden />
        <StatusPill variant={bentoStatusVariant(cell.status)} hideDot size="sm">
          {bentoStatusLabel(cell.status)}
        </StatusPill>
      </div>
      <h3 className="mt-4 text-base font-semibold text-foreground">{cell.title}</h3>
      <p className="mt-1.5 text-sm leading-6 text-muted-foreground">{cell.body}</p>
      {visual()}
    </Link>
  );
}
