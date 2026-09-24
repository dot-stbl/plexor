/**
 * Managed-database fixtures — the catalog bounded context.
 *
 * Топология: pve-rack1-n02 — Docker-хост, pve-rack2-n01 — single-node k8s,
 * pve-rack1-n01 даёт VM/LXC. availability: vm→pve-rack1-n01,pve-rack1-n02;
 * lxc→pve-rack1-n01,pve-rack1-n02; docker→pve-rack1-n02; k8s→pve-rack2-n01.
 */
import type { DbEngine, DbCluster, RuntimeHost } from '@/domains/catalog';

const HOSTS: RuntimeHost[] = [
  { nodeId: 'node-pve-r1n01', hostname: 'pve-rack1-n01', runtimes: ['vm', 'lxc'] },
  { nodeId: 'node-pve-r1n02', hostname: 'pve-rack1-n02', runtimes: ['vm', 'lxc', 'docker'] },
  { nodeId: 'node-pve-r2n01', hostname: 'pve-rack2-n01', runtimes: ['k8s'] },
];

/**
 * Предустановленные движки БД. Разреженная матрица validRuntimes:
 *   - Postgres/Redis/Garnet — без k8s (нет shipped-оператора → direct).
 *   - ClickHouse/Kafka — С k8s (есть операторы: Altinity / Strimzi, delegated).
 */
const ENGINES: DbEngine[] = [
  {
    id: 'postgres',
    name: 'PostgreSQL',
    kind: 'relational',
    version: '16',
    source: 'docker.io/library/postgres:16',
    validRuntimes: ['vm', 'lxc', 'docker'],
    connstring: 'postgres://{user}:{pass}@{dns}:5432/{db}',
    supportsBackups: true,
    blurb: 'Default relational database. WAL backups, PITR.',
    about:
      'PostgreSQL is a reliable relational database for transactional workloads. The instance runs on the runtime you choose (VM, LXC, or Docker); Plexor handles networking, internal DNS, and backups (WAL + point-in-time recovery).',
  },
  {
    id: 'redis',
    name: 'Redis',
    kind: 'cache',
    version: '7.4',
    source: 'docker.io/library/redis:7.4',
    validRuntimes: ['vm', 'lxc', 'docker'],
    connstring: 'redis://{dns}:6379',
    supportsBackups: true,
    blurb: 'Cache and data structures. RDB/AOF snapshots.',
    about:
      'Redis is an in-memory store for caching, sessions, and queues. Fast startup, RDB/AOF snapshots. Plexor joins the instance to the shared mesh and hands out the binding string to consumers automatically.',
  },
  {
    id: 'garnet',
    name: 'Garnet',
    kind: 'cache',
    version: '1.0',
    source: 'ghcr.io/microsoft/garnet',
    validRuntimes: ['vm', 'lxc', 'docker'],
    connstring: 'redis://{dns}:6379',
    supportsBackups: true,
    blurb: 'Redis-compatible cache from Microsoft, higher throughput.',
    about:
      'Garnet is a Redis-compatible cache from Microsoft with higher throughput on multi-core nodes. A drop-in Redis replacement: same protocol, same clients, but it scales vertically better.',
  },
  {
    id: 'clickhouse',
    name: 'ClickHouse',
    kind: 'analytics',
    version: '24.8',
    source: 'Altinity operator / clickhouse-server',
    validRuntimes: ['vm', 'docker', 'k8s'],
    connstring: 'clickhouse://{dns}:9000',
    supportsBackups: true,
    blurb: 'Columnar analytics. On k8s — Altinity operator.',
    about:
      'ClickHouse is a columnar database for real-time analytical queries over large volumes. On k8s it deploys via the Altinity operator; on VM and Docker, directly. Ideal for dashboards and logs.',
  },
  {
    id: 'kafka',
    name: 'Apache Kafka',
    kind: 'queue',
    version: '3.7',
    source: 'Strimzi operator / kafka',
    validRuntimes: ['vm', 'docker', 'k8s'],
    connstring: '{dns}:9092',
    supportsBackups: false,
    blurb: 'Event streaming. On k8s — Strimzi operator.',
    about:
      'Apache Kafka is a distributed event log for streaming and service integration. On k8s it runs via the Strimzi operator; Plexor places brokers across the cluster nodes. Guarantees message ordering and durability.',
  },
];

/**
 * Развёрнутые кластеры — по всем рантаймам. PostgreSQL намеренно оставлен
 * пустым, чтобы демонстрировать онбординг-`EmptyState` раздела.
 */
const CLUSTERS: DbCluster[] = [
  {
    id: 'db-redis-cache',
    name: 'redis-cache',
    engineId: 'redis',
    kind: 'cache',
    runtime: 'docker',
    nodeId: 'node-pve-r1n02',
    hostname: 'pve-rack1-n02',
    dns: 'redis-cache.db.plexor.internal',
    status: 'running',
    version: '7.4',
    storageGb: 8,
    backupsEnabled: false,
    bindings: 2,
    createdAt: '2026-07-01T09:05:00Z',
  },
  {
    id: 'db-garnet-sessions',
    name: 'garnet-sessions',
    engineId: 'garnet',
    kind: 'cache',
    runtime: 'lxc',
    nodeId: 'node-pve-r1n01',
    hostname: 'pve-rack1-n01',
    dns: 'garnet-sessions.db.plexor.internal',
    status: 'running',
    version: '1.0',
    storageGb: 16,
    backupsEnabled: false,
    bindings: 2,
    createdAt: '2026-07-03T12:00:00Z',
  },
  {
    id: 'db-ch-events',
    name: 'ch-events',
    engineId: 'clickhouse',
    kind: 'analytics',
    runtime: 'k8s',
    nodeId: 'node-pve-r2n01',
    hostname: 'pve-rack2-n01',
    dns: 'ch-events.db.plexor.internal',
    status: 'running',
    version: '24.8',
    storageGb: 500,
    backupsEnabled: true,
    bindings: 1,
    createdAt: '2026-06-25T08:00:00Z',
  },
  {
    id: 'db-kafka-bus',
    name: 'kafka-bus',
    engineId: 'kafka',
    kind: 'queue',
    runtime: 'k8s',
    nodeId: 'node-pve-r2n01',
    hostname: 'pve-rack2-n01',
    dns: 'kafka-bus.db.plexor.internal',
    status: 'deploying',
    version: '3.7',
    storageGb: 100,
    backupsEnabled: false,
    bindings: 0,
    createdAt: '2026-07-08T08:40:00Z',
  },
  {
    id: 'db-redis-queue',
    name: 'redis-queue',
    engineId: 'redis',
    kind: 'cache',
    runtime: 'vm',
    nodeId: 'node-a',
    hostname: 'node-a.local',
    dns: 'redis-queue.db.plexor.internal',
    status: 'degraded',
    version: '7.4',
    storageGb: 16,
    backupsEnabled: true,
    bindings: 3,
    createdAt: '2026-07-15T10:20:00Z',
  },
  {
    id: 'db-ch-logs',
    name: 'ch-logs',
    engineId: 'clickhouse',
    kind: 'analytics',
    runtime: 'docker',
    nodeId: 'node-b',
    hostname: 'node-b.local',
    dns: 'ch-logs.db.plexor.internal',
    status: 'stopped',
    version: '24.8',
    storageGb: 120,
    backupsEnabled: false,
    bindings: 0,
    createdAt: '2026-07-20T16:45:00Z',
  },
];

export function listRuntimeHosts(): RuntimeHost[] {
  return HOSTS;
}

export function listEngines(): DbEngine[] {
  return ENGINES;
}

export function getEngine(id: string): DbEngine | undefined {
  return ENGINES.find((e) => e.id === id);
}

export function listDbClusters(): DbCluster[] {
  return CLUSTERS;
}
