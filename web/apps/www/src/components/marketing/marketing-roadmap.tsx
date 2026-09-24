import { PhaseItem, type RoadmapPhase } from './phase-item';

/**
 * Marketing roadmap — vertical timeline of every shipped and planned
 * Plexor version. Each row answers "what can an operator actually do
 * with this version when it ships?" rather than listing internal
 * module names, schema names or migration steps.
 *
 * The source of truth is `openspec/changes/` and `.agents/docs/scope.md`;
 * this list is curated from those, not invented from scratch. Every
 * entry is one merged change or one open proposal — no fiction.
 */
const PHASES: readonly RoadmapPhase[] = [
  {
    id: 'v0.1',
    version: 'v0.1',
    title: 'First running cluster',
    status: 'shipped',
    shipped: [
      'One Plexor binary, one process to run',
      'Log in, create an organization, set up your first users',
      'Console shell up — you can see what the binary has provisioned',
      'Three built-in palettes; pick one per organization',
    ],
    planned: [],
  },
  {
    id: 'v0.2',
    version: 'v0.2',
    title: 'Compute, network and storage',
    status: 'shipped',
    shipped: [
      'Virtual machines with snapshots and an in-browser console',
      'Private networks, subnets, security groups, floating IPs and load balancers',
      'Block volumes and S3-compatible object storage',
      'Single-node path that runs on a single server without a storage cluster',
    ],
    planned: [],
  },
  {
    id: 'v0.3',
    version: 'v0.3',
    title: 'App catalog and usage tracking',
    status: 'next',
    shipped: [],
    planned: [
      'Install Postgres, Redis, Keycloak, WordPress and Ghost with one command',
      'Operators write their own catalog entries — drop in a manifest, ship it to your team',
      'Usage tracking that rolls up into an invoice viewable from the console',
      'Per-organization and per-folder quotas for memory, CPU and storage',
    ],
  },
  {
    id: 'v0.4',
    version: 'v0.4',
    title: 'Community palettes',
    status: 'next',
    shipped: [],
    planned: [
      'Browse, install and activate palettes contributed by the community',
      'Each install verifies its manifest against a signing key held by the host',
      'Live preview before activating — see what your console will look like',
    ],
  },
  {
    id: 'v0.5',
    version: 'v0.5',
    title: 'Managed Kubernetes and containers',
    status: 'design',
    shipped: [],
    planned: [
      'Bring up a Kubernetes cluster from the console as an app install',
      'Run a container registry alongside your workloads',
      'Scheduled backups that respect quotas, and quotas that know about backups',
    ],
  },
  {
    id: 'v0.6',
    version: 'v0.6',
    title: 'Spreading across regions',
    status: 'design',
    shipped: [],
    planned: [
      'Run Plexor in two regions with one console in front of both',
      'Planned failover between regions when the primary goes down',
      'Stream the audit log to whatever SIEM you already operate',
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
            What is shipping now, and what is next.
          </h2>
          <p className="text-sm leading-6 text-muted-foreground">
            Each row is one merged release or one open proposal —
            nothing speculative. Operators running Plexor today see a
            version number that matches a row in this list.
          </p>
        </header>

        <ol className="space-y-3">
          {PHASES.map((phase) => (
            <PhaseItem
              key={phase.id}
              phase={phase}
              shippedLabel="In the box"
              plannedLabel="Coming next"
            />
          ))}
        </ol>
      </div>
    </section>
  );
}
