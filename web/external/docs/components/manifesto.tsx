import type { ReactNode } from 'react';
import { ArrowOutward } from '@nine-thirty-five/material-symbols-react/rounded/700';

/**
 * Two-column section — "What we don't ship" on the left, "What we do instead"
 * on the right. The asymmetry is the point: every refused pattern has a
 * concrete replacement that names a real Plexor feature.
 *
 * Each row is a `×` refusal paired with a `✓` alternative. Typography only;
 * no badges or extra icons — the words have to do the work. Sourced from
 * `.agents/docs/architecture.md`, `openspec/specs/branding/spec.md`,
 * `openspec/changes/fe-themes-branding/proposal.md`.
 */
interface Row {
  readonly no: string;
  readonly yes: string;
}

const ROWS_RU: readonly Row[] = [
  {
    no: 'OpenStack-grade микросервисный зоопарк — 30+ бинарей для деплоя, мониторинга, обновлений и координации.',
    yes: 'Один Plexor.Host бинарь. Все модули — один процесс. Реплики, когда измеренная нагрузка требует.',
  },
  {
    no: 'Kubernetes как runtime-требование для деплоя приложений.',
    yes: 'Обычный Podman/Docker run на каждой compute-ноде. App providers — это shell, не Helm.',
  },
  {
    no: 'Helm-чарты, которые дрифтуют между версиями и форками.',
    yes: 'provider.yaml — версионированный, HMAC-подписанный манифест. Install + upgrade = одна команда.',
  },
  {
    no: 'Принудительный SaaS control plane, account lock-in, vendor-managed secrets.',
    yes: 'Своё железо, свой Keycloak, свой Ceph. Plexor работает на том, что у вас есть.',
  },
  {
    no: 'UI в пяти местах: console, marketplace, docs, billing, audit.',
    yes: 'Одна консоль. Marketplace, audit, metering — табы. Без портала-порталов.',
  },
  {
    no: 'Отдельный billing portal, который звонит домой по любому поводу.',
    yes: 'Опциональный модуль Metering + Invoice. Выключен по умолчанию. Никакого egress.',
  },
];

const ROWS_EN: readonly Row[] = [
  {
    no: 'OpenStack-grade microservice sprawl — 30+ binaries to deploy, monitor, patch, and coordinate.',
    yes: 'One Plexor.Host binary. All modules, one process. Replicas when measured load demands.',
  },
  {
    no: 'Kubernetes as a runtime requirement for app deployment.',
    yes: 'A plain Podman/Docker run on each compute node. App providers are shell, not Helm.',
  },
  {
    no: 'Helm charts that drift across versions and forks.',
    yes: 'A provider.yaml is a versioned, HMAC-signed manifest. Install + upgrade is one command.',
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

const COPY = {
  ru: {
    kicker: 'Манифест',
    title: 'Что мы не делаем',
    subtitle:
      'Self-hosted cloud не должен быть копией AWS. Список того, от чего мы отказались, и что предлагаем взамен.',
    noLabel: 'Не делаем',
    yesLabel: 'Делаем',
    footnote: (
      <>
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
      </>
    ),
  },
  en: {
    kicker: 'Manifesto',
    title: "What we don't ship",
    subtitle:
      "A self-hosted cloud shouldn't be a copy of AWS. Six things we refused, and what we offer instead.",
    noLabel: "Don't",
    yesLabel: 'Do',
    footnote: (
      <>
        Every refusal is a deliberate decision, recorded in the{' '}
        <a
          href="/en/concepts/resource-scope"
          className="text-fd-foreground underline decoration-fd-muted-foreground/40 underline-offset-4 hover:decoration-fd-foreground"
        >
          architecture docs
        </a>
        . Disagree?{' '}
        <a
          href="https://github.com/dot-stbl/plexor/issues"
          className="text-fd-foreground inline-flex items-center gap-1 underline decoration-fd-muted-foreground/40 underline-offset-4 hover:decoration-fd-foreground"
        >
          open an issue
          <ArrowOutward className="size-3" aria-hidden />
        </a>
        .
      </>
    ),
  },
} as const;

export function Manifesto({ locale = 'ru' }: { locale?: 'ru' | 'en' } = {}) {
  const rows = locale === 'en' ? ROWS_EN : ROWS_RU;
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

      <div className="grid grid-cols-1 gap-x-8 gap-y-3 md:grid-cols-[1fr_24px_1fr]">
        <ColumnHeader side="no">{copy.noLabel}</ColumnHeader>
        <div aria-hidden />
        <ColumnHeader side="yes">{copy.yesLabel}</ColumnHeader>

        {rows.map((row) => (
          <RowItem key={row.no} row={row} />
        ))}
      </div>

      <Footnote>{copy.footnote}</Footnote>
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
        'text-fd-muted-foreground text-xs font-medium uppercase tracking-[0.16em] md:pt-1 ' +
        (side === 'yes' ? 'md:text-right' : '')
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