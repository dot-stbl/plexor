/**
 * Audit fixtures — the audit bounded context. A fixed set of historical
 * entries (deterministic, referencing REAL fleet/cluster/token/user ids
 * from the other bounded contexts) plus a live in-memory log that
 * `db/store.ts` appends to on every VM mutation, so `/admin/audit`
 * shows real actions as they happen. Newest-first.
 */
import type { AuditQueryResponse } from '@/shared/api';
import { MOCK_TIMESTAMP } from '../db/seed-config';
import { FLEET } from '../compute/vms';
import { CLUSTERS } from '../fleet/clusters';
import { USERS } from '../identity/users';

const ORG_ID = '00000000-0000-0000-0000-000000000001';
const NOW = Date.parse(MOCK_TIMESTAMP);
const HOUR = 60 * 60 * 1000;

const HUMAN_USER = USERS.find((u) => u.kind === 'human')!.id;
const SERVICE_IAM = USERS.find((u) => u.id === 'service-iam')!.id;
const TOKENS = CLUSTERS[0]!.tokens;
const ACTIVE_TOKEN_ID = TOKENS.find((t) => t.status === 'active')?.id ?? TOKENS[0]!.id;
const EXPIRED_TOKEN_ID = TOKENS.find((t) => t.status === 'expired')?.id ?? TOKENS[0]!.id;

interface HistoricalRow {
  action: string;
  actorUserId: string;
  targetKind: string;
  targetId: string;
}

const CYCLE: HistoricalRow[] = [
  { action: 'vm.start', actorUserId: HUMAN_USER, targetKind: 'vm', targetId: FLEET[0]!.id },
  { action: 'vm.stop', actorUserId: HUMAN_USER, targetKind: 'vm', targetId: FLEET[6]!.id },
  { action: 'vm.create', actorUserId: HUMAN_USER, targetKind: 'vm', targetId: FLEET[7]!.id },
  { action: 'vm.delete', actorUserId: HUMAN_USER, targetKind: 'vm', targetId: FLEET[5]!.id },
  { action: 'cluster.token.issue', actorUserId: HUMAN_USER, targetKind: 'token', targetId: ACTIVE_TOKEN_ID },
  { action: 'cluster.token.revoke', actorUserId: SERVICE_IAM, targetKind: 'token', targetId: EXPIRED_TOKEN_ID },
  { action: 'branding.update', actorUserId: HUMAN_USER, targetKind: 'branding', targetId: 'branding:global' },
  { action: 'auth.login', actorUserId: HUMAN_USER, targetKind: 'session', targetId: `session:${HUMAN_USER}` },
  { action: 'auth.logout', actorUserId: HUMAN_USER, targetKind: 'session', targetId: `session:${HUMAN_USER}` },
];

function seedHistorical(count: number): AuditQueryResponse[] {
  const entries: AuditQueryResponse[] = [];
  for (let index = 0; index < count; index += 1) {
    const row = CYCLE[index % CYCLE.length]!;
    entries.push({
      id: `audit-seed-${index.toString().padStart(4, '0')}`,
      occurredAt: new Date(NOW - index * HOUR).toISOString(),
      action: row.action,
      orgId: ORG_ID,
      actorUserId: row.actorUserId,
      targetKind: row.targetKind,
      targetId: row.targetId,
      payload: {},
    });
  }
  return entries;
}

const HISTORICAL: AuditQueryResponse[] = seedHistorical(24);
let live: AuditQueryResponse[] = [];
let liveSeq = 0;

export interface LiveAuditInput {
  action: string;
  actorUserId: string;
  targetKind: string;
  targetId: string;
  payload?: Record<string, unknown>;
}

/** Called by `db/store.ts` mutations — prepends a live row so it renders
 *  as the most recent audit entry. */
export function appendAuditEntry(entry: LiveAuditInput): void {
  liveSeq += 1;
  live = [
    {
      id: `audit-live-${liveSeq.toString().padStart(4, '0')}`,
      occurredAt: new Date().toISOString(),
      action: entry.action,
      orgId: ORG_ID,
      actorUserId: entry.actorUserId,
      targetKind: entry.targetKind,
      targetId: entry.targetId,
      payload: entry.payload ?? {},
    },
    ...live,
  ];
}

export interface AuditQueryFilter {
  action?: string;
  actorUserId?: string;
  since?: string;
  before?: string;
  limit?: number;
}

/** Filter + page the combined live+historical log — mirrors the real
 *  API's `GetAuditQueryParams` (action/actorUserId/since/before/limit). */
export function queryAuditEntries(filter: AuditQueryFilter = {}): AuditQueryResponse[] {
  let rows: AuditQueryResponse[] = [...live, ...HISTORICAL];
  if (filter.action) rows = rows.filter((r) => r.action === filter.action);
  if (filter.actorUserId) rows = rows.filter((r) => r.actorUserId === filter.actorUserId);
  if (filter.since) {
    const t = Date.parse(filter.since);
    rows = rows.filter((r) => Date.parse(r.occurredAt) >= t);
  }
  if (filter.before) {
    const t = Date.parse(filter.before);
    rows = rows.filter((r) => Date.parse(r.occurredAt) < t);
  }
  const limit = filter.limit ?? 100;
  return rows.slice(0, limit);
}

/** Back-compat for existing callers (launcher summary) that just want
 *  N deterministic historical rows without filtering. */
export function makeAuditEntries(count = 12): AuditQueryResponse[] {
  return HISTORICAL.slice(0, count);
}

/** Test-only reset for the live log (does not touch HISTORICAL). */
export function resetAuditLog(): void {
  live = [];
  liveSeq = 0;
}
