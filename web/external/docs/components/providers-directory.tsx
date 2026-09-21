import { ArrowOutward } from '@nine-thirty-five/material-symbols-react/rounded/700';

/**
 * Providers Directory — three columns, each answering a different
 * operator concern. Pulled from `providers.md` and `openspec/specs/clusters/spec.md`:
 *
 * 1. Install providers — selected at `plx init` via system probes. The
 *    installer probes the host (KVM? OVS? Ceph? MinIO?) and recommends.
 * 2. Always required — Postgres (state DB) and NATS (event bus) ship
 *    with every Plexor install. Not selectable; just there.
 * 3. App providers — YAML + shell manifests, installed post-init via
 *    `plx provider install <source>`. Community-maintained.
 */
interface Provider {
  readonly id: string;
  readonly label: string;
  readonly note: string;
  readonly source?: string;
}

const INSTALL_PROVIDERS: readonly Provider[] = [
  {
    id: 'kvm',
    label: 'KVM',
    note: 'Compute default. Linux + /dev/kvm + libvirtd + VT-x.',
    source: 'src/providers/Plexor.Providers.Compute.Kvm/',
  },
  {
    id: 'ovs',
    label: 'OVS',
    note: 'Network default. Multi-VM overlay через Open vSwitch.',
    source: 'src/providers/Plexor.Providers.Network.Ovs/',
  },
  {
    id: 'ceph-rbd',
    label: 'Ceph RBD',
    note: 'Block storage default. Multi-node с replication.',
    source: 'src/providers/Plexor.Providers.Storage.Ceph/',
  },
  {
    id: 'ceph-rgw',
    label: 'Ceph RGW',
    note: 'Object storage default. S3-compatible для VM-снапшотов и buckets.',
    source: 'src/providers/Plexor.Providers.Storage.Ceph/',
  },
  {
    id: 'local-lvm',
    label: 'Local LVM',
    note: 'Single-node dev path. Thinpool на root VG.',
  },
  {
    id: 'minio',
    label: 'MinIO',
    note: 'Single-node S3 fallback. Когда не Ceph.',
  },
];

const ALWAYS_REQUIRED: readonly Provider[] = [
  {
    id: 'postgresql',
    label: 'PostgreSQL',
    note: 'State DB. Хранит все модули (realm, sigil, atlas, ...). Всегда required.',
  },
  {
    id: 'nats',
    label: 'NATS JetStream',
    note: 'Event bus. Command-events (control → node) и status-events (node → control).',
  },
];

const APP_PROVIDERS: readonly Provider[] = [
  {
    id: 'app-postgresql',
    label: 'PostgreSQL',
    note: 'Standalone 15+. Авто-resolve через provider.yaml.',
  },
  {
    id: 'app-redis',
    label: 'Redis',
    note: 'Standalone 7+. Caching + session store.',
  },
  {
    id: 'app-nginx',
    label: 'Nginx',
    note: 'Reverse proxy / ingress. Альтернатива HAProxy.',
  },
  {
    id: 'app-minio',
    label: 'MinIO',
    note: 'S3-compatible для user data (отдельно от system object store).',
  },
  {
    id: 'app-keycloak',
    label: 'Keycloak',
    note: 'OAuth/OIDC для self-hosted identity federation.',
  },
  {
    id: 'app-wordpress',
    label: 'WordPress',
    note: 'Reference CMS provider. Запускается за 30 секунд.',
  },
  {
    id: 'app-ghost',
    label: 'Ghost',
    note: 'Modern CMS. Тот же install/upgrade flow, другой runtime.',
  },
];

const COPY = {
  ru: {
    kicker: 'Providers',
    title: 'Что устанавливается вместе с Plexor и что добавляется сверху',
    subtitle: (
      <>
        Install providers живут внутри{' '}
        <code className="font-mono">Plexor.Host</code> — выбираются при{' '}
        <code className="font-mono">plx init</code>. App providers — это
        YAML-манифесты с shell-хуками, дистрибуция через git/OCI/tarball.
      </>
    ),
    installTitle: 'Install providers',
    installSubtitle: 'Built-in код в Plexor.Host · не плагины',
    requiredTitle: 'Always required',
    requiredSubtitle: 'Идут в комплекте с каждой установкой',
    appTitle: 'App providers',
    appSubtitle: 'YAML + shell · community-maintained',
    footnote: (
      <>
        Хотите добавить свой install provider? Это новый проект в{' '}
        <code className="font-mono">src/providers/</code>, не NuGet. Хотите
        свой app provider? Это{' '}
        <a
          href="/marketplace/authoring-provider"
          className="text-fd-foreground underline decoration-fd-muted-foreground/40 underline-offset-4 hover:decoration-fd-foreground"
        >
          YAML-файл
        </a>
        , не плагин.
      </>
    ),
  },
  en: {
    kicker: 'Providers',
    title: 'What ships with Plexor and what gets added on top',
    subtitle: (
      <>
        Install providers live inside{' '}
        <code className="font-mono">Plexor.Host</code> — selected at{' '}
        <code className="font-mono">plx init</code>. App providers are
        YAML manifests with shell hooks, distributed via git/OCI/tarball.
      </>
    ),
    installTitle: 'Install providers',
    installSubtitle: 'Built-in code in Plexor.Host · not plugins',
    requiredTitle: 'Always required',
    requiredSubtitle: 'Ship with every install',
    appTitle: 'App providers',
    appSubtitle: 'YAML + shell · community-maintained',
    footnote: (
      <>
        Want your own install provider? A new project in{' '}
        <code className="font-mono">src/providers/</code>, not a NuGet.
        Want your own app provider?{' '}
        <a
          href="/en/marketplace/authoring-provider"
          className="text-fd-foreground underline decoration-fd-muted-foreground/40 underline-offset-4 hover:decoration-fd-foreground"
        >
          A YAML file
        </a>
        , not a plugin.
      </>
    ),
  },
} as const;

export function ProvidersDirectory({
  locale = 'ru',
}: { locale?: 'ru' | 'en' } = {}) {
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

      <div className="grid grid-cols-1 gap-6 lg:grid-cols-3">
        <ProviderColumn
          title={copy.installTitle}
          subtitle={copy.installSubtitle}
          providers={INSTALL_PROVIDERS}
        />
        <ProviderColumn
          title={copy.requiredTitle}
          subtitle={copy.requiredSubtitle}
          providers={ALWAYS_REQUIRED}
        />
        <ProviderColumn
          title={copy.appTitle}
          subtitle={copy.appSubtitle}
          providers={APP_PROVIDERS}
        />
      </div>

      <p className="text-fd-muted-foreground mt-4 text-xs">{copy.footnote}</p>
    </div>
  );
}

function ProviderColumn({
  title,
  subtitle,
  providers,
}: {
  title: string;
  subtitle: string;
  providers: readonly Provider[];
}) {
  return (
    <div className="border-fd-border bg-fd-card rounded-xl border p-5">
      <div className="mb-4 border-b border-fd-border pb-3">
        <h3 className="m-0 text-base font-semibold">{title}</h3>
        <p className="text-fd-muted-foreground mt-0.5 text-xs">{subtitle}</p>
      </div>
      <ul className="space-y-3">
        {providers.map((provider) => (
          <ProviderRow key={provider.id} provider={provider} />
        ))}
      </ul>
    </div>
  );
}

function ProviderRow({ provider }: { provider: Provider }) {
  return (
    <li className="flex items-start gap-3">
      <code className="bg-fd-muted text-fd-foreground mt-0.5 shrink-0 rounded px-1.5 py-0.5 font-mono text-xs">
        {provider.id}
      </code>
      <div className="min-w-0">
        <div className="flex items-baseline gap-2">
          <span className="font-medium">{provider.label}</span>
          {provider.source === undefined ? null : (
            <a
              href={`https://github.com/dot-stbl/plexor/tree/develop/${provider.source}`}
              className="text-fd-muted-foreground hover:text-fd-foreground inline-flex items-center gap-1 text-xs"
            >
              source
              <ArrowOutward className="size-3" aria-hidden />
            </a>
          )}
        </div>
        <p className="text-fd-muted-foreground text-xs">{provider.note}</p>
      </div>
    </li>
  );
}