import type { ReactNode } from 'react';
import { ArrowOutward } from '@nine-thirty-five/material-symbols-react/rounded/700';

/**
 * Two-column section — "What we don't ship" on the left, "What we do instead"
 * on the right. The asymmetry is the point: every refused pattern has a
 * concrete replacement that names a real Plexor feature.
 *
 * Reads top-to-bottom; each row is one refusal + one alternative. No
 * icons, no badges — typography only. The pattern is too easy to dress up
 * with decorative noise; we want the words to do the work.
 */
interface Row {
  readonly no: string;
  readonly yes: string;
}

const ROWS: readonly Row[] = [
  {
    no: 'OpenStack-grade microservice sprawl — 30+ binaries to deploy, monitor, patch, and coordinate.',
    yes: 'One Plexor.Host binary. All modules, one process. Replicas when you actually need them.',
  },
  {
    no: 'Kubernetes as a runtime requirement for app deployment.',
    yes: 'A plain Podman/Docker run on each compute node. App providers are shell, not Helm.',
  },
  {
    no: 'Helm charts that drift across versions and forks.',
    yes: 'A `provider.yaml` is a versioned, HMAC-signed manifest. Install + upgrade is one command.',
  },
  {
    no: 'Forced SaaS control plane, account lock-in, vendor-managed secrets.',
    yes: 'Bring your own hardware, your own Keycloak, your own Ceph. Plexor runs on what you have.',
  },
  {
    no: 'UI in five places: console, marketplace, docs, billing, audit.',
    yes: 'One console. Marketplace, audit, metering — tabs. No portal-of-portals.',
  },
  {
    no: 'A separate billing portal that phones home for everything.',
    yes: 'Optional Metering + Invoice module. Disabled by default. No egress.',
  },
];

export function Manifesto() {
  return (
    <div className="not-prose my-10">
      <header className="mb-6 max-w-2xl">
        <p className="text-fd-muted-foreground mb-2 text-xs font-medium uppercase tracking-[0.18em]">
          Манифест
        </p>
        <h2 className="mb-2 text-3xl font-semibold tracking-tight">
          Что мы не делаем
        </h2>
        <p className="text-fd-muted-foreground text-sm">
          Self-hosted cloud не должен быть копией AWS. Список того, от чего мы
          отказались, и что предлагаем взамен.
        </p>
      </header>

      <div className="grid grid-cols-1 gap-x-8 gap-y-3 md:grid-cols-[1fr_24px_1fr]">
        <ColumnHeader side="no">Не делаем</ColumnHeader>
        <div aria-hidden />
        <ColumnHeader side="yes">Делаем</ColumnHeader>

        {ROWS.map((row) => (
          <RowItem key={row.no} row={row} />
        ))}
      </div>

      <Footnote>
        Каждый отказ — это осознанное решение, зафиксированное в{' '}
        <a
          href="/concepts/resource-scope"
          className="text-fd-foreground underline decoration-fd-muted-foreground/40 underline-offset-4 hover:decoration-fd-foreground"
        >
          architecture docs
        </a>
        . Спорить можно —{' '}
        <a
          href="https://github.com/dot-stbl/plexor/issues"
          className="text-fd-foreground inline-flex items-center gap-1 underline decoration-fd-muted-foreground/40 underline-offset-4 hover:decoration-fd-foreground"
        >
          открывайте issue
          <ArrowOutward className="size-3" aria-hidden />
        </a>
        .
      </Footnote>
    </div>
  );
}

function ColumnHeader({
  side,
  children,
}: {
  side: 'no' | 'yes';
  children: ReactNode;
}) {
  return (
    <div
      className={
        side === 'no'
          ? 'text-fd-err-ink text-xs font-medium uppercase tracking-[0.18em] md:pt-1'
          : 'text-fd-ok-ink text-xs font-medium uppercase tracking-[0.18em] md:pt-1'
      }
    >
      {children}
    </div>
  );
}

function RowItem({ row }: { row: Row }) {
  return (
    <>
      <div className="border-fd-border/60 border-b py-4 pr-4">
        <p className="text-fd-muted-foreground text-sm leading-relaxed">
          <span className="text-fd-err mr-2 font-medium">×</span>
          {row.no}
        </p>
      </div>
      <div aria-hidden className="hidden md:block" />
      <div className="border-fd-border/60 border-b py-4 pl-4">
        <p className="text-sm leading-relaxed">
          <span className="text-fd-ok mr-2 font-medium">✓</span>
          {row.yes}
        </p>
      </div>
    </>
  );
}

function Footnote({ children }: { children: ReactNode }) {
  return (
    <p className="text-fd-muted-foreground mt-6 max-w-2xl text-xs">
      {children}
    </p>
  );
}