import { ArrowOutward } from '@nine-thirty-five/material-symbols-react/rounded/700';

/**
 * Providers Directory — two columns, install providers and app providers,
 * each row carrying what it does and where its code lives. Pulled from
 * `.agents/docs/providers.md` and `openspec/specs/clusters/spec.md`; not
 * invented. Operators come here to see what they can actually deploy today
 * and what's a roadmap item.
 *
 * Install providers are built into the host binary (no plugin model). App
 * providers are YAML + shell, distributed via git/OCI/tarball — see the
 * `authoring-provider` doc for the format.
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
  {
    id: 'postgresql',
    label: 'PostgreSQL',
    note: 'State DB. Всегда required.',
  },
  {
    id: 'nats',
    label: 'NATS JetStream',
    note: 'Event bus. Всегда required.',
  },
];

const APP_PROVIDERS: readonly Provider[] = [
  {
    id: 'postgresql',
    label: 'PostgreSQL',
    note: 'Standalone 15+. Авто-resolve через provider.yaml.',
  },
  {
    id: 'redis',
    label: 'Redis',
    note: 'Standalone 7+. Caching + session store.',
  },
  {
    id: 'nginx',
    label: 'Nginx',
    note: 'Reverse proxy / ingress. Альтернатива HAProxy.',
  },
  {
    id: 'minio',
    label: 'MinIO',
    note: 'S3-compatible для user data (отдельно от system object store).',
  },
  {
    id: 'keycloak',
    label: 'Keycloak',
    note: 'OAuth/OIDC для self-hosted identity federation.',
  },
  {
    id: 'wordpress',
    label: 'WordPress',
    note: 'Reference CMS provider. Запускается за 30 секунд.',
  },
  {
    id: 'ghost',
    label: 'Ghost',
    note: 'Modern CMS. Тот же install/upgrade flow, другой runtime.',
  },
];

export function ProvidersDirectory() {
  return (
    <div className="not-prose my-10">
      <header className="mb-6 max-w-2xl">
        <p className="text-fd-muted-foreground mb-2 text-xs font-medium uppercase tracking-[0.18em]">
          Providers
        </p>
        <h2 className="mb-2 text-3xl font-semibold tracking-tight">
          Что устанавливается вместе с Plexor и что добавляется сверху
        </h2>
        <p className="text-fd-muted-foreground text-sm">
          Install providers живут внутри{' '}
          <code className="font-mono">Plexor.Host</code> — выбираются при{' '}
          <code className="font-mono">plx init</code>. App providers — это
          YAML-манифесты с shell-хуками, дистрибуция через git/OCI/tarball.
        </p>
      </header>

      <div className="grid grid-cols-1 gap-6 lg:grid-cols-2">
        <ProviderColumn
          title="Install providers"
          subtitle="Built-in код в Plexor.Host · не плагины"
          providers={INSTALL_PROVIDERS}
        />
        <ProviderColumn
          title="App providers"
          subtitle="YAML + shell · community-maintained"
          providers={APP_PROVIDERS}
        />
      </div>

      <p className="text-fd-muted-foreground mt-4 text-xs">
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
      </p>
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

