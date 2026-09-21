/**
 * Marketing manifesto — a two-column list of refused patterns (×) and
 * the Plexor replacement (✓). The asymmetry is the point: every row
 * answers "what do you actually do instead?" rather than just
 * enumerating what we don't ship.
 *
 * Visual: three-column grid on md+, with the "Не делаем" / "Делаем"
 * headers aligned over their columns and a thin hairline running
 * under each row. Mobile collapses to a single column with both cells
 * stacked.
 */
interface Row {
  readonly no: string;
  readonly yes: string;
}

const ROWS: readonly Row[] = [
  {
    no: 'OpenStack-grade микросервисный зоопарк — 30+ бинарей для деплоя, мониторинга, обновлений и координации.',
    yes: 'Один Plexor.Host бинарь. Все модули — один процесс. Реплики, когда измеренная нагрузка требует.',
  },
  {
    no: 'Kubernetes как runtime-требование для деплоя приложений.',
    yes: 'Обычный Podman/Docker run на каждой compute-ноде. App providers — это shell, не Helm.',
  },
  {
    no: 'Helм-чарты, которые дрифтуют между версиями и форками.',
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

export function MarketingManifesto() {
  return (
    <section className="border-b border-border">
      <div className="mx-auto max-w-7xl px-6 py-16 md:py-20">
        <header className="mb-8 max-w-2xl">
          <p className="mb-2 font-mono text-[11px] font-medium uppercase tracking-[0.16em] text-muted-2">
            Манифест
          </p>
          <h2 className="mb-2 text-3xl font-semibold tracking-tight text-foreground">
            Что мы не делаем
          </h2>
          <p className="text-sm leading-6 text-muted-foreground">
            Self-hosted cloud не должен быть копией AWS. Список того, от
            чего мы отказались, и что предлагаем взамен.
          </p>
        </header>

        <div className="grid grid-cols-1 gap-x-8 md:grid-cols-[1fr_1fr]">
          <div className="mb-2 font-mono text-[11px] font-medium uppercase tracking-[0.16em] text-muted-2 md:pt-1">
            Не делаем
          </div>
          <div className="mb-2 font-mono text-[11px] font-medium uppercase tracking-[0.16em] text-muted-2 md:pt-1 md:text-right">
            Делаем
          </div>

          {ROWS.map((row) => (
            <RowPair key={row.no} row={row} />
          ))}
        </div>
      </div>
    </section>
  );
}

function RowPair({ row }: { row: Row }) {
  return (
    <>
      <div className="border-b border-border/60 py-4 pr-4">
        <p className="text-sm leading-6 text-muted-foreground">
          <span className="mr-2 font-medium text-err">×</span>
          {row.no}
        </p>
      </div>
      <div className="border-b border-border/60 py-4 pl-4">
        <p className="text-sm leading-6 text-foreground">
          <span className="mr-2 font-medium text-ok">✓</span>
          {row.yes}
        </p>
      </div>
    </>
  );
}