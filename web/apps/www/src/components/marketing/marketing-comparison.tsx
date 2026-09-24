import { StatusPill } from '@/components/ui/status-pill';
import { EYEBROW_CLASS } from '@/components/chrome/panel';

type Cell = string | { readonly variant: 'warn' | 'idle'; readonly label: string };

interface ComparisonRow {
  readonly feature: string;
  readonly proxmox: Cell;
  readonly openstack: Cell;
  readonly plexor: Cell;
}

/**
 * Honest comparison table (spec §3.7) — corrected per amendment A5
 * (Proxmox VE natively clusters and ships built-in RBAC; the original
 * spec table understated both). At least two rows admit a real Plexor
 * gap today: multi-node is "Planned", and maturity is "Pre-stable" — a
 * third (managed app catalog) is also honestly "Planned", not claimed
 * shipped, per the roadmap's own v0.3/"next" status for it.
 *
 * Two renderings, not one table with a scroll container: a real 4-column
 * `<table>` at `md:` and up, and a stacked label/value card per row below
 * `md` — a fixed-width table forced into a 390px viewport either forces
 * horizontal scroll or crushes the text unreadably; a hard "no horizontal
 * scroll" requirement rules the first out, and squeezing four columns of
 * mixed-length prose to ~90px each rules out the second.
 */
const ROWS: readonly ComparisonRow[] = [
  { feature: 'Single-box install', proxmox: '✓', openstack: 'Heavy (many services)', plexor: '✓' },
  {
    feature: 'Multi-node',
    proxmox: '✓ native clustering',
    openstack: '✓',
    plexor: { variant: 'warn', label: 'Planned' },
  },
  {
    feature: 'Multi-tenant orgs / teams / folders',
    proxmox: 'Limited (pools + permissions)',
    openstack: '✓ (projects/domains)',
    plexor: '✓',
  },
  {
    feature: 'Managed app catalog (DBs, caches, IdP)',
    proxmox: 'Templates only',
    openstack: 'Separate projects (Trove etc.)',
    plexor: { variant: 'warn', label: 'Planned' },
  },
  {
    feature: 'Maturity',
    proxmox: 'Years in production',
    openstack: 'Years in production',
    plexor: { variant: 'idle', label: 'Pre-stable, v0.x' },
  },
];

function CellValue({ value }: { value: Cell }) {
  if (typeof value === 'string') {
    return <span className="text-sm text-muted-foreground">{value}</span>;
  }
  return (
    <StatusPill variant={value.variant} hideDot size="sm">
      {value.label}
    </StatusPill>
  );
}

export function MarketingComparison() {
  return (
    <div>
      <p className={EYEBROW_CLASS}>Honest comparison</p>
      <h2 className="mt-3 max-w-2xl text-3xl font-extrabold tracking-tight text-foreground">
        Where Plexor is strong — and where it isn&apos;t yet.
      </h2>

      {/* Below md: one stacked card per row. */}
      <div className="mt-8 space-y-3 md:hidden">
        {ROWS.map((row) => (
          <div key={row.feature} className="rounded-2xl border border-border bg-card p-4">
            <p className="text-sm font-medium text-foreground">{row.feature}</p>
            <dl className="mt-3 space-y-2">
              <div className="flex items-center justify-between gap-3">
                <dt className="text-xs text-muted-2">Proxmox VE</dt>
                <dd>
                  <CellValue value={row.proxmox} />
                </dd>
              </div>
              <div className="flex items-center justify-between gap-3">
                <dt className="text-xs text-muted-2">OpenStack</dt>
                <dd>
                  <CellValue value={row.openstack} />
                </dd>
              </div>
              <div className="flex items-center justify-between gap-3">
                <dt className="text-xs text-muted-2">Plexor (today)</dt>
                <dd>
                  <CellValue value={row.plexor} />
                </dd>
              </div>
            </dl>
          </div>
        ))}
      </div>

      {/* md and up: the real table. */}
      <div className="mt-8 hidden rounded-2xl border border-border md:block">
        <table className="w-full text-left">
          <thead>
            <tr className="border-b border-border bg-card">
              <th className="px-4 py-3 text-xs font-semibold text-foreground">Feature</th>
              <th className="px-4 py-3 text-xs font-semibold text-foreground">Proxmox VE</th>
              <th className="px-4 py-3 text-xs font-semibold text-foreground">OpenStack</th>
              <th className="px-4 py-3 text-xs font-semibold text-foreground">Plexor (today)</th>
            </tr>
          </thead>
          <tbody>
            {ROWS.map((row) => (
              <tr key={row.feature} className="border-b border-border/60 last:border-0">
                <td className="px-4 py-3 text-sm font-medium text-foreground">{row.feature}</td>
                <td className="px-4 py-3">
                  <CellValue value={row.proxmox} />
                </td>
                <td className="px-4 py-3">
                  <CellValue value={row.openstack} />
                </td>
                <td className="px-4 py-3">
                  <CellValue value={row.plexor} />
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </div>
  );
}
