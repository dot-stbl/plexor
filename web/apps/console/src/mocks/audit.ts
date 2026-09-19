/**
 * Audit fixtures — shared by MSW handlers (`shared/api/mocks/handlers.ts`)
 * and the launcher SUMMARY card.
 *
 * Each entry matches `AuditQueryResponse` from `@/shared/api`. The
 * handler-level MSW response wraps a list of these entries; tests
 * reading from `audit.ts` get the same shape.
 *
 * Note: the kubb-generated `AuditQueryResponse` doesn't expose a
 * friendly `actor`/`target` pair — it has `actorUserId`, `targetKind`,
 * and `targetId`. We build the friendlier display strings at the
 * launcher / page level from those IDs.
 */

import type { AuditQueryResponse } from '@/shared/api';

const ACTIONS = [
  'vm.start',
  'vm.stop',
  'vm.delete',
  'vm.create',
  'cluster.token.issue',
  'cluster.token.revoke',
  'branding.update',
  'auth.login',
  'auth.logout',
] as const;
const ACTOR_IDS = ['user-dev-1', 'service-control-plane', 'service-iam'] as const;
const TARGET_KINDS = ['vm', 'cluster', 'token', 'branding', 'session'] as const;
const TARGET_IDS = [
  'vm-a8c91f2e',
  'vm-b7d40e1a',
  'vm-c2f8a039',
  'cluster-prod-eu',
  'tok-2026-07-13-001',
  'branding:global',
  'session:user-dev-1',
] as const;

/** Build N audit entries, deterministic for stable screenshots. */
export function makeAuditEntries(count = 12): AuditQueryResponse[] {
  const now = Date.parse('2026-09-19T10:00:00Z');
  const oneHour = 60 * 60 * 1000;
  const entries: AuditQueryResponse[] = [];
  for (let index = 0; index < count; index += 1) {
    const actionIndex = index % ACTIONS.length;
    const actorIndex = index % ACTOR_IDS.length;
    const kindIndex = index % TARGET_KINDS.length;
    const targetIndex = index % TARGET_IDS.length;
    entries.push({
      id: `audit-${index.toString().padStart(4, '0')}`,
      occurredAt: new Date(now - index * oneHour).toISOString(),
      action: ACTIONS[actionIndex] ?? ACTIONS[0]!,
      orgId: '00000000-0000-0000-0000-000000000001',
      actorUserId: ACTOR_IDS[actorIndex] ?? ACTOR_IDS[0]!,
      targetKind: TARGET_KINDS[kindIndex] ?? TARGET_KINDS[0]!,
      targetId: TARGET_IDS[targetIndex] ?? TARGET_IDS[0]!,
      payload: { index },
    });
  }
  return entries;
}
