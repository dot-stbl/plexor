/**
 * Managed data services (базы данных) — UI mock.
 *
 * Postgres/Redis/Garnet/ClickHouse/Kafka — это НЕ безымянные сервисы, а
 * сложные managed-движки: бэкапы, тонкая настройка, HA. Живут в разделе
 * «Платформа данных». Под капотом — та же runtime-модель из
 * `.agents/docs/architecture/runtimes-and-bindings.md` (движок отвязан от
 * рантайма VM/LXC/Docker/k8s; placement = validRuntimes ∩ availableRuntimes).
 *
 * NOT kubb-generated: локальные типы + mock + локальные хуки.
 */

/* ─────────────── Runtime model (generic, переиспользуемо) ─────────────── */

export type Runtime = 'vm' | 'lxc' | 'docker' | 'k8s';
export type RuntimeClass = 'direct' | 'delegated';

export const RUNTIME_ORDER = ['vm', 'lxc', 'docker', 'k8s'] as const satisfies readonly Runtime[];

export const RUNTIME_META: Record<Runtime, { label: string; class: RuntimeClass; blurb: string }> = {
  vm: { label: 'VM', class: 'direct', blurb: 'KVM — full isolation, own kernel' },
  lxc: { label: 'LXC', class: 'direct', blurb: 'System container, lightweight' },
  docker: { label: 'Docker', class: 'direct', blurb: 'App container, fast startup' },
  k8s: { label: 'k8s', class: 'delegated', blurb: 'Delegated to the cluster (operator)' },
};

export interface RuntimeHost {
  nodeId: string;
  hostname: string;
  runtimes: Runtime[];
}

export interface RuntimeOption {
  runtime: Runtime;
  available: boolean;
  valid: boolean;
  enabled: boolean;
  reason?: string;
  nodes: string[];
}

export function availableRuntimes(hosts: readonly RuntimeHost[]): Set<Runtime> {
  const set = new Set<Runtime>();
  for (const h of hosts) for (const r of h.runtimes) set.add(r);
  return set;
}

/** valid ∩ available для всех 4 рантаймов + причина, если нельзя. */
export function runtimeOptions(engine: DbEngine, hosts: readonly RuntimeHost[]): RuntimeOption[] {
  return RUNTIME_ORDER.map((runtime) => {
    const nodes = hosts.filter((h) => h.runtimes.includes(runtime)).map((h) => h.hostname);
    const available = nodes.length > 0;
    const valid = engine.validRuntimes.includes(runtime);
    let reason: string | undefined;
    if (!valid) {
      reason =
        runtime === 'k8s'
          ? 'no k8s recipe (operator) for this engine'
          : 'engine does not support this runtime';
    } else if (!available) {
      reason = 'no node with this runtime';
    }
    return { runtime, available, valid, enabled: valid && available, reason, nodes };
  });
}

/** Умный дефолт: direct-рантаймы приоритетнее делегированного k8s. */
export function defaultRuntime(options: readonly RuntimeOption[]): Runtime | undefined {
  const preference: Runtime[] = ['docker', 'lxc', 'vm', 'k8s'];
  for (const r of preference) {
    const opt = options.find((o) => o.runtime === r);
    if (opt?.enabled) return opt.runtime;
  }
  return options.find((o) => o.enabled)?.runtime;
}

/* ─────────────── Database domain ─────────────── */

export type DbKind = 'relational' | 'cache' | 'queue' | 'analytics';
export type DbStatus = 'running' | 'deploying' | 'degraded' | 'stopped' | 'error';

export const DB_KIND_LABEL: Record<DbKind, string> = {
  relational: 'Relational',
  cache: 'Cache',
  queue: 'Queue',
  analytics: 'Analytics',
};

/**
 * Каталожный движок БД — «предустановленный» blessed-движок. `source` —
 * занятый готовый артефакт/оператор (транслятор). `validRuntimes` —
 * разреженная матрица: где есть k8s-оператор, там k8s заполнен.
 */
export interface DbEngine {
  id: string;
  name: string;
  kind: DbKind;
  version: string;
  /** Занятый готовый артефакт (образ / оператор). */
  source: string;
  validRuntimes: Runtime[];
  /** binding-контракт для потребителей. */
  connstring: string;
  /** Managed-движки умеют бэкапы. */
  supportsBackups: boolean;
  /** Короткая строка (таблица, подпись). */
  blurb: string;
  /** Развёрнутое описание для онбординг/empty-state (2-3 предложения). */
  about: string;
}

/** Развёрнутый кластер БД (managed-деплой: один или несколько хостов). */
export interface DbCluster {
  id: string;
  name: string;
  engineId: string;
  kind: DbKind;
  runtime: Runtime;
  nodeId: string;
  hostname: string;
  dns: string;
  status: DbStatus;
  version: string;
  storageGb: number;
  backupsEnabled: boolean;
  /** Сколько потребителей привязано (bindings). */
  bindings: number;
  createdAt: string;
}

export function mapDbStatusToVariant(status: DbStatus):
  | 'running' | 'pending' | 'warn' | 'stopped' | 'failed' {
  switch (status) {
    case 'running': return 'running';
    case 'deploying': return 'pending';
    case 'degraded': return 'warn';
    case 'stopped': return 'stopped';
    case 'error': return 'failed';
  }
}
