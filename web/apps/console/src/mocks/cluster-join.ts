// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// Mock handlers for the cluster / join API. Pure functions returning
// in-memory data — no fetch, no server. Wire-up at `use-join.ts` calls
// these directly until real kubb-generated hooks are in place.
//
// Data shape mirrors what the plan/clusters OpenAPI endpoints will
// return (see `.agents/plans/plan-clusters/PLAN.md`). Until those
// endpoints ship, these mocks are the source of truth for the UI.
// ==========================================================================

import type { JoinToken, NodeRole } from '@/features/clusters/cluster-types';

const PLEXOR_VERSION = '0.1.0-dev';

/** Fake token store — what `POST /api/v1/compute/clusters/{id}/tokens`
 *  would return in production. The active token is the one currently
 *  consumable via the join URL; older tokens are shown as revoked. */
const FAKE_TOKENS: JoinToken[] = [
  {
    id: 'tok-2026-07-13-001',
    label: 'node-1 (prod-eu-1)',
    status: 'active',
    token: 'plx_jtok_aB3x...f7',
    intendedRole: 'compute' as NodeRole,
    minIsoVersion: PLEXOR_VERSION,
    issuedAt: '2026-07-12T08:00:00Z',
    expiresAt: '2026-07-20T08:00:00Z',
  },
  {
    id: 'tok-2026-07-10-002',
    label: 'gpu-1 (staging-eu-1)',
    status: 'revoked',
    token: 'plx_jtok_ReVo...ed1',
    intendedRole: 'compute' as NodeRole,
    minIsoVersion: PLEXOR_VERSION,
    issuedAt: '2026-07-08T08:00:00Z',
    expiresAt: '2026-07-15T08:00:00Z',
    redeemedByNodeId: 'node-2026-07-09-001',
  },
  {
    id: 'tok-2026-07-12-003',
    label: 'control-1 (staging-eu-1)',
    status: 'expired',
    token: 'plx_jtok_ExPi...ed2',
    intendedRole: 'control' as NodeRole,
    minIsoVersion: PLEXOR_VERSION,
    issuedAt: '2026-06-30T08:00:00Z',
    expiresAt: '2026-07-12T08:00:00Z',
  },
];

/** Issue a new join token. Pretends to call
 *  `POST /api/v1/compute/clusters/{id}/tokens` with a generated UUID
 *  and the standard 7-day TTL. */
export function mockIssueToken(
  clusterId: string,
  args: { label: string; intendedRole: NodeRole; ttlDays?: number },
): JoinToken {
  const now = new Date();
  const ttl = (args.ttlDays ?? 7) * 24 * 60 * 60 * 1000;
  const token: JoinToken = {
    id: `tok-${Date.now().toString(36)}`,
    label: args.label,
    status: 'active',
    token: `plx_jtok_${Math.random().toString(16).slice(2, 34)}`,
    intendedRole: args.intendedRole,
    minIsoVersion: PLEXOR_VERSION,
    issuedAt: now.toISOString(),
    expiresAt: new Date(now.getTime() + ttl).toISOString(),
  };
  FAKE_TOKENS.unshift(token);
  return token;
}

/** Revoke a token. Mirrors `POST /api/v1/compute/clusters/{id}/tokens/{tid}/revoke`. */
export function mockRevokeToken(clusterId: string, tokenId: string): void {
  const t = FAKE_TOKENS.find((x) => x.id === tokenId);
  if (t) t.status = 'revoked';
}

/** List tokens for a cluster. Mirrors
 *  `GET /api/v1/compute/clusters/{id}/tokens`. */
export function mockListTokens(clusterId: string): JoinToken[] {
  return FAKE_TOKENS.slice();
}
