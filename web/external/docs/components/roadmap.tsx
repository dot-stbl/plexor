import type { ReactNode } from 'react';
import { Check } from '@nine-thirty-five/material-symbols-react/rounded/700';

/**
 * Roadmap timeline — what's shipped now, what's next, what's still in design.
 * Sourced from `openspec/changes/` and `.agents/docs/scope.md`; not invented.
 * Each phase shows the headline capability and one or two concrete deliverables.
 *
 * The visual is a horizontal step rail — denser than vertical roadmap posters,
 * less decorative than the typical SaaS roadmap with rocket icons. Operators
 * come here to see what's real and what's planned, not to be impressed.
 */
type Status = 'shipped' | 'next' | 'design';

interface Phase {
  readonly id: string;
  readonly version: string;
  readonly title: string;
  readonly status: Status;
  readonly shipped: readonly string[];
  readonly planned: readonly string[];
}

const PHASES: readonly Phase[] = [
  {
    id: 'v0.1',
    version: 'v0.1',
    title: 'Modular monolith',
    status: 'shipped',
    shipped: [
      'Plexor.Host binary, single deploy',
      'Tenants · Identity · Audit modules',
      'OpenAPI source-generator + Kubb client',
      'BootConfig + per-org branding',
      'Console shell + 3 first-party themes',
    ],
    planned: [],
  },
  {
    id: 'v0.2',
    version: 'v0.2',
    title: 'Compute, Network, Storage',
    status: 'shipped',
    shipped: [
      'KVM / OVS / Ceph install providers',
      'Local-lvm + MinIO single-node path',
      'VM lifecycle, snapshots, noVNC console',
      'VPC + subnets + SGs + floating IPs + LB',
      'Block volumes + S3 buckets',
    ],
    planned: [],
  },
  {
    id: 'v0.3',
    version: 'v0.3',
    title: 'Marketplace + metering',
    status: 'next',
    shipped: [],
    planned: [
      'App-provider install flow (YAML + shell)',
      'Provider catalog (Postgres, Redis, Keycloak, WordPress, Ghost)',
      'Metering events → hourly rollups → invoice',
      'Per-tenant and per-project quotas',
    ],
  },
  {
    id: 'v0.4',
    version: 'v0.4',
    title: 'Theme marketplace',
    status: 'next',
    shipped: [],
    planned: [
      'ThemeInstallation entity + HMAC manifest verification',
      'Admin install UI for community themes',
      'Per-org activation + atomic single-active',
      'Custom-CSS escape hatch (16 KiB cap)',
    ],
  },
  {
    id: 'v0.5',
    version: 'v0.5',
    title: 'Managed Kubernetes',
    status: 'design',
    shipped: [],
    planned: [
      'k8s-cluster app provider on the cluster',
      'Container registry as an app provider',
      'Velero-based backup',
    ],
  },
  {
    id: 'v0.6',
    version: 'v0.6',
    title: 'Multi-region + audit export',
    status: 'design',
    shipped: [],
    planned: [
      'Postgres logical replication + BDR',
      'Cross-region failover',
      'SIEM export (audit → olfs provider)',
    ],
  },
];

function statusLabel(status: Status): string {
  switch (status) {
    case 'shipped':
      return 'Готово';
    case 'next':
      return 'В работе';
    case 'design':
      return 'В дизайне';
  }
}

function statusClasses(status: Status): string {
  switch (status) {
    case 'shipped':
      return 'border-fd-ok/30 bg-fd-ok-soft text-fd-ok-ink';
    case 'next':
      return 'border-fd-warn/30 bg-fd-warn-soft text-fd-warn-ink';
    case 'design':
      return 'border-fd-idle/30 bg-fd-idle-soft text-fd-idle-ink';
  }
}

export function Roadmap() {
  return (
    <div className="not-prose my-10">
      <header className="mb-6 max-w-2xl">
        <p className="text-fd-muted-foreground mb-2 text-xs font-medium uppercase tracking-[0.18em]">
          Roadmap
        </p>
        <h2 className="mb-2 text-3xl font-semibold tracking-tight">
          Что готово и что дальше
        </h2>
        <p className="text-fd-muted-foreground text-sm">
          Источник — <code className="font-mono">openspec/changes/</code> и{' '}
          <code className="font-mono">.agents/docs/scope.md</code>. Без фантазий:
          каждая фаза — это либо merged change, либо open proposal.
        </p>
      </header>

      <ol className="relative space-y-3">
        {PHASES.map((phase) => (
          <li
            key={phase.id}
            className="border-fd-border bg-fd-card relative rounded-xl border p-5"
          >
            <PhaseHeader phase={phase} />
            <PhaseBody phase={phase} />
          </li>
        ))}
      </ol>
    </div>
  );
}

function PhaseHeader({ phase }: { phase: Phase }) {
  return (
    <div className="mb-3 flex items-baseline justify-between gap-3">
      <div className="flex items-baseline gap-3">
        <code className="bg-fd-muted text-fd-foreground rounded px-1.5 py-0.5 font-mono text-xs">
          {phase.version}
        </code>
        <h3 className="m-0 text-lg font-semibold">{phase.title}</h3>
      </div>
      <span
        className={
          'shrink-0 rounded-full border px-2 py-0.5 text-[0.7rem] font-medium uppercase tracking-wider ' +
          statusClasses(phase.status)
        }
      >
        {statusLabel(phase.status)}
      </span>
    </div>
  );
}

function PhaseBody({ phase }: { phase: Phase }) {
  if (phase.shipped.length === 0 && phase.planned.length === 0) return null;

  return (
    <div className="grid grid-cols-1 gap-3 text-sm md:grid-cols-2">
      {phase.shipped.length > 0 ? (
        <BulletList
          items={phase.shipped}
          icon={<Check className="text-fd-ok size-3.5 shrink-0" aria-hidden />}
          label="Уже в коробке"
        />
      ) : null}
      {phase.planned.length > 0 ? (
        <BulletList
          items={phase.planned}
          icon={<span className="text-fd-muted-foreground text-base leading-none">·</span>}
          label="Планируется"
        />
      ) : null}
    </div>
  );
}

function BulletList({
  items,
  icon,
  label,
}: {
  items: readonly string[];
  icon: ReactNode;
  label: string;
}) {
  return (
    <div>
      <div className="text-fd-muted-foreground mb-1.5 text-[0.7rem] font-medium uppercase tracking-[0.18em]">
        {label}
      </div>
      <ul className="space-y-1.5">
        {items.map((item) => (
          <li key={item} className="flex items-start gap-2">
            <span className="mt-1 inline-flex">{icon}</span>
            <span>{item}</span>
          </li>
        ))}
      </ul>
    </div>
  );
}