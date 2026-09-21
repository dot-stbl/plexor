import { PhaseItem, type RoadmapPhase } from './phase-item';

/**
 * Marketing roadmap — vertical timeline of every shipped and planned
 * Plexor phase. The data is local on purpose: every change ships a
 * new entry, not a content edit to a registry. The source of truth is
 * `openspec/changes/` + `.agents/docs/scope.md`; this list is curated
 * from those, not the other way around.
 */
const PHASES: readonly RoadmapPhase[] = [
  {
    id: 'v0.1',
    version: 'v0.1',
    title: 'Modular monolith',
    status: 'shipped',
    shipped: [
      'Plexor.Host бинарь, один деплой',
      'Модули Tenants · Identity · Audit',
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
      'Install providers KVM / OVS / Ceph',
      'Single-node путь Local LVM + MinIO',
      'Lifecycle VM, snapshots, noVNC console',
      'VPC + subnets + SG + FIP + LB',
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
      'Install-flow app providers (YAML + shell)',
      'Каталог providers (Postgres, Redis, Keycloak, WordPress, Ghost)',
      'Metering events → hourly rollups → invoice',
      'Per-tenant и per-project квоты',
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
      'Admin install UI для community themes',
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
      'k8s-cluster app provider на кластере',
      'Container registry как app provider',
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

export function MarketingRoadmap() {
  return (
    <section className="border-b border-border">
      <div className="mx-auto max-w-7xl px-6 py-16 md:py-20">
        <header className="mb-8 max-w-2xl">
          <p className="mb-2 font-mono text-[11px] font-medium uppercase tracking-[0.16em] text-muted-2">
            Roadmap
          </p>
          <h2 className="mb-2 text-3xl font-semibold tracking-tight text-foreground">
            Что готово и что дальше
          </h2>
          <p className="text-sm leading-6 text-muted-foreground">
            Источник — <code className="font-mono text-foreground">openspec/changes/</code> и{' '}
            <code className="font-mono text-foreground">.agents/docs/scope.md</code>. Без
            фантазий: каждая фаза — это либо merged change, либо open proposal.
          </p>
        </header>

        <ol className="space-y-3">
          {PHASES.map((phase) => (
            <PhaseItem
              key={phase.id}
              phase={phase}
              shippedLabel="Уже в коробке"
              plannedLabel="Планируется"
            />
          ))}
        </ol>
      </div>
    </section>
  );
}