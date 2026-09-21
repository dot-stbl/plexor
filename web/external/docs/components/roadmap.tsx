import { Check } from '@nine-thirty-five/material-symbols-react/rounded/700';

/**
 * Roadmap timeline — what's shipped now, what's next, what's still in design.
 * Sourced from `openspec/changes/` and `.agents/docs/scope.md`; not invented.
 * Each phase shows the headline capability and one or two concrete deliverables.
 *
 * The visual is a vertical step rail — denser than vertical roadmap posters,
 * less decorative than the typical SaaS roadmap with rocket icons. Operators
 * come here to see what's real and what's planned, not to be impressed.
 */
type Status = 'shipped' | 'next' | 'design';

interface Phase {
  readonly id: string;
  readonly version: string;
  readonly title: { ru: string; en: string };
  readonly status: Status;
  readonly shipped: { ru: string; en: string }[];
  readonly planned: { ru: string; en: string }[];
}

const PHASES: readonly Phase[] = [
  {
    id: 'v0.1',
    version: 'v0.1',
    title: {
      ru: 'Modular monolith',
      en: 'Modular monolith',
    },
    status: 'shipped',
    shipped: [
      {
        ru: 'Plexor.Host бинарь, один деплой',
        en: 'Plexor.Host binary, single deploy',
      },
      {
        ru: 'Модули Tenants · Identity · Audit',
        en: 'Tenants · Identity · Audit modules',
      },
      {
        ru: 'OpenAPI source-generator + Kubb client',
        en: 'OpenAPI source-generator + Kubb client',
      },
      {
        ru: 'BootConfig + per-org branding',
        en: 'BootConfig + per-org branding',
      },
      {
        ru: 'Console shell + 3 first-party themes',
        en: 'Console shell + 3 first-party themes',
      },
    ],
    planned: [],
  },
  {
    id: 'v0.2',
    version: 'v0.2',
    title: {
      ru: 'Compute, Network, Storage',
      en: 'Compute, Network, Storage',
    },
    status: 'shipped',
    shipped: [
      {
        ru: 'Install providers KVM / OVS / Ceph',
        en: 'KVM / OVS / Ceph install providers',
      },
      {
        ru: 'Single-node путь Local LVM + MinIO',
        en: 'Local-lvm + MinIO single-node path',
      },
      {
        ru: 'Lifecycle VM, snapshots, noVNC console',
        en: 'VM lifecycle, snapshots, noVNC console',
      },
      {
        ru: 'VPC + subnets + SG + FIP + LB',
        en: 'VPC + subnets + SGs + floating IPs + LB',
      },
      {
        ru: 'Block volumes + S3 buckets',
        en: 'Block volumes + S3 buckets',
      },
    ],
    planned: [],
  },
  {
    id: 'v0.3',
    version: 'v0.3',
    title: {
      ru: 'Marketplace + metering',
      en: 'Marketplace + metering',
    },
    status: 'next',
    shipped: [],
    planned: [
      {
        ru: 'Install-flow app providers (YAML + shell)',
        en: 'App-provider install flow (YAML + shell)',
      },
      {
        ru: 'Каталог providers (Postgres, Redis, Keycloak, WordPress, Ghost)',
        en: 'Provider catalog (Postgres, Redis, Keycloak, WordPress, Ghost)',
      },
      {
        ru: 'Metering events → hourly rollups → invoice',
        en: 'Metering events → hourly rollups → invoice',
      },
      {
        ru: 'Per-tenant и per-project квоты',
        en: 'Per-tenant and per-project quotas',
      },
    ],
  },
  {
    id: 'v0.4',
    version: 'v0.4',
    title: {
      ru: 'Theme marketplace',
      en: 'Theme marketplace',
    },
    status: 'next',
    shipped: [],
    planned: [
      {
        ru: 'ThemeInstallation entity + HMAC manifest verification',
        en: 'ThemeInstallation entity + HMAC manifest verification',
      },
      {
        ru: 'Admin install UI для community themes',
        en: 'Admin install UI for community themes',
      },
      {
        ru: 'Per-org activation + atomic single-active',
        en: 'Per-org activation + atomic single-active',
      },
      {
        ru: 'Custom-CSS escape hatch (16 KiB cap)',
        en: 'Custom-CSS escape hatch (16 KiB cap)',
      },
    ],
  },
  {
    id: 'v0.5',
    version: 'v0.5',
    title: {
      ru: 'Managed Kubernetes',
      en: 'Managed Kubernetes',
    },
    status: 'design',
    shipped: [],
    planned: [
      {
        ru: 'k8s-cluster app provider на кластере',
        en: 'k8s-cluster app provider on the cluster',
      },
      {
        ru: 'Container registry как app provider',
        en: 'Container registry as an app provider',
      },
      {
        ru: 'Velero-based backup',
        en: 'Velero-based backup',
      },
    ],
  },
  {
    id: 'v0.6',
    version: 'v0.6',
    title: {
      ru: 'Multi-region + audit export',
      en: 'Multi-region + audit export',
    },
    status: 'design',
    shipped: [],
    planned: [
      {
        ru: 'Postgres logical replication + BDR',
        en: 'Postgres logical replication + BDR',
      },
      {
        ru: 'Cross-region failover',
        en: 'Cross-region failover',
      },
      {
        ru: 'SIEM export (audit → olfs provider)',
        en: 'SIEM export (audit → olfs provider)',
      },
    ],
  },
];

const STATUS_LABEL = {
  shipped: { ru: 'Готово', en: 'Shipped' },
  next: { ru: 'В работе', en: 'In progress' },
  design: { ru: 'В дизайне', en: 'In design' },
} as const;

const STATUS_CLASSES = {
  shipped: 'border-fd-ok/30 bg-fd-ok-soft text-fd-ok-ink',
  next: 'border-fd-warn/30 bg-fd-warn-soft text-fd-warn-ink',
  design: 'border-fd-warn/30 bg-fd-warn-soft text-fd-warn-ink',
} as const;

const COPY = {
  ru: {
    kicker: 'Roadmap',
    title: 'Что готово и что дальше',
    subtitle: (
      <>
        Источник — <code className="font-mono">openspec/changes/</code> и{' '}
        <code className="font-mono">.agents/docs/scope.md</code>. Без фантазий:
        каждая фаза — это либо merged change, либо open proposal.
      </>
    ),
    shippedLabel: 'Уже в коробке',
    plannedLabel: 'Планируется',
  },
  en: {
    kicker: 'Roadmap',
    title: "What's shipped and what's next",
    subtitle: (
      <>
        Sourced from <code className="font-mono">openspec/changes/</code>{' '}
        and <code className="font-mono">.agents/docs/scope.md</code>. No
        fantasies: every phase is either a merged change or an open proposal.
      </>
    ),
    shippedLabel: 'Shipped',
    plannedLabel: 'Planned',
  },
} as const;

export function Roadmap({ locale = 'ru' }: { locale?: 'ru' | 'en' } = {}) {
  const copy = COPY[locale];

  return (
    <div className="landing-block not-prose my-10">
      <header className="mb-6 max-w-2xl">
        <p className="text-fd-muted-foreground mb-2 text-xs font-medium uppercase tracking-[0.16em]">
          {copy.kicker}
        </p>
        <h2 className="mb-2 text-3xl font-semibold tracking-tight">
          {copy.title}
        </h2>
        <p className="text-fd-muted-foreground text-sm">{copy.subtitle}</p>
      </header>

      <ol className="relative space-y-3">
        {PHASES.map((phase) => (
          <PhaseItem key={phase.id} phase={phase} locale={locale} copy={copy} />
        ))}
      </ol>
    </div>
  );
}

function PhaseItem({
  phase,
  locale,
  copy,
}: {
  phase: Phase;
  locale: 'ru' | 'en';
  copy: (typeof COPY)[keyof typeof COPY];
}) {
  return (
    <li className="border-fd-border bg-fd-card relative rounded-xl border p-5">
      <PhaseHeader phase={phase} locale={locale} />
      <PhaseBody phase={phase} locale={locale} copy={copy} />
    </li>
  );
}

function PhaseHeader({
  phase,
  locale,
}: {
  phase: Phase;
  locale: 'ru' | 'en';
}) {
  return (
    <div className="mb-3 flex items-baseline justify-between gap-3">
      <div className="flex items-baseline gap-3">
        <code className="bg-fd-muted text-fd-foreground rounded px-1.5 py-0.5 font-mono text-xs">
          {phase.version}
        </code>
        <h3 className="m-0 text-lg font-semibold">{phase.title[locale]}</h3>
      </div>
      <span
        className={
          'shrink-0 rounded-full border px-2 py-0.5 text-[0.7rem] font-medium uppercase tracking-wider ' +
          STATUS_CLASSES[phase.status]
        }
      >
        {STATUS_LABEL[phase.status][locale]}
      </span>
    </div>
  );
}

function PhaseBody({
  phase,
  locale,
  copy,
}: {
  phase: Phase;
  locale: 'ru' | 'en';
  copy: (typeof COPY)[keyof typeof COPY];
}) {
  if (phase.shipped.length === 0 && phase.planned.length === 0) return null;

  return (
    <div className="grid grid-cols-1 gap-3 text-sm md:grid-cols-2">
      {phase.shipped.length > 0 ? (
        <BulletList
          items={phase.shipped.map((entry) => entry[locale])}
          icon={<Check className="text-fd-ok size-3.5 shrink-0" aria-hidden />}
          label={copy.shippedLabel}
        />
      ) : null}
      {phase.planned.length > 0 ? (
        <BulletList
          items={phase.planned.map((entry) => entry[locale])}
          icon={<span className="text-fd-muted-foreground text-base leading-none">·</span>}
          label={copy.plannedLabel}
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
  icon: React.ReactNode;
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