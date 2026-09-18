// MSW request handlers — composed from kubb-generated per-operation factories,
// fed with kubb-generated faker fixtures.
//
// Coverage:
//   29 handlers come from `bun run generate` (kubb); see
//   `web/tooling/codegen/kubb.config.ts`.
//   3 handlers (`getBrandingTheme`, `updateBrandingTheme`,
//   `deleteBrandingTheme`) are hand-mirrored — kubb 4.39.2 skipped them in
//   the MSW + faker pass; see `msw/getBrandingThemeHandler.ts` for the kz
//   note. Survives codegen `clean: true` (this file lives outside `./src`).
//
// The OpenAPI contract is the source of truth for endpoint shapes — when
// adding a new endpoint, regenerate via `web/tooling/codegen` and add one
// line below (or, if kubb skipped it like /branding/theme, hand-mirror
// the handler + fixture and add a `kz` note).
import type { RequestHandler } from 'msw';
import { faker } from '@faker-js/faker';
import {
  // VM endpoints (6)
  listVmsHandler,
  getVmHandler,
  provisionVmHandler,
  startVmHandler,
  stopVmHandler,
  deleteVmHandler,
  // Node endpoints (4)
  nodeJoinHandler,
  nodeHeartbeatHandler,
  nodeCommandPollHandler,
  nodeCommandResultHandler,
  // Quota endpoints (5)
  getQuotaDefinitionsHandler,
  listQuotaAssignmentsHandler,
  upsertQuotaAssignmentHandler,
  deleteQuotaAssignmentHandler,
  listQuotaUsageHandler,
  listEffectiveQuotasHandler,
  // Branding endpoints (6 + theme hand-mirrored)
  getBrandingGlobalHandler,
  updateBrandingGlobalHandler,
  getBrandingBootHandler,
  getBrandingOrgHandler,
  updateBrandingOrgHandler,
  deleteBrandingOrgHandler,
  getBrandingThemeHandler,
  updateBrandingThemeHandler,
  deleteBrandingThemeHandler,
  // Audit (1)
  getAuditHandler,
  // Auth providers (3)
  getOrgAuthProviderHandler,
  updateOrgAuthProviderHandler,
  testOrgAuthProviderHandler,
  // OIDC (3 — see note below)
  getOidcAuthorizeHandler,
  getOidcCallbackHandler,
  postOidcLogoutHandler,
  // Fixtures
  createVmList,
  createVmDetail,
  createNodeJoinResponse,
  createNodeHeartbeat200,
  createNodeCommandPollResponse,
  createNodeCommandResult,
  createQuotaDefinitionSummary,
  createQuotaAssignmentSummary,
  createEffectiveQuotaEntry,
  createGlobalThemeConfigResponse,
  createBrandingBootConfig,
  createOrgBrandingConfigResponse,
  createGetBrandingTheme200,
  createOrgAuthProviderConfigResponse,
  createOrgAuthProviderTestResult,
} from '@/shared/api';

// Deterministic mocks — same data every reload (stable UI + screenshots).
faker.seed(1337);

// Hand-curated fleet so the list renders a realistic mix of statuses.
// Names + IDs + IPs are deterministic; the rest comes from kubb factories.
const FLEET = [
  { id: 'vm-a8c91f2e', name: 'web-prod-01', status: 'running',      ip: '10.128.1.10', zone: 'eu-central-1', vcpu: 4, ram: 8,  disk: 80  },
  { id: 'vm-b7d40e1a', name: 'api-prod-01', status: 'running',      ip: '10.128.1.11', zone: 'eu-central-1', vcpu: 4, ram: 8,  disk: 60  },
  { id: 'vm-c2f8a039', name: 'worker-01',    status: 'running',      ip: '10.128.2.20', zone: 'eu-central-1', vcpu: 2, ram: 4,  disk: 40  },
  { id: 'vm-9e1b3c47', name: 'db-replica-01',status: 'running',      ip: '10.128.3.5',  zone: 'eu-central-1', vcpu: 8, ram: 32, disk: 500 },
  { id: 'vm-3a7c5d12', name: 'cache-01',     status: 'running',      ip: '10.128.4.7',  zone: 'eu-central-1', vcpu: 2, ram: 16, disk: 30  },
  { id: 'vm-6f8d22b8', name: 'build-runner', status: 'error',        ip: '10.128.5.3',  zone: 'eu-central-1', vcpu: 4, ram: 8,  disk: 100 },
  { id: 'vm-1b9e4f73', name: 'staging-api',  status: 'stopped',      ip: '10.128.6.12', zone: 'eu-central-1', vcpu: 2, ram: 4,  disk: 40  },
  { id: 'vm-4d2a89e1', name: 'ml-trainer',   status: 'provisioning', ip: '10.128.7.4',  zone: 'eu-central-1', vcpu: 8, ram: 64, disk: 250 },
  { id: 'vm-7c2e91a4', name: 'edge-amsterdam',status: 'running',     ip: '10.128.8.21', zone: 'eu-west-1',    vcpu: 4, ram: 16, disk: 120 },
  { id: 'vm-0a5b8d63', name: 'edge-singapore',status: 'running',     ip: '10.128.9.5',  zone: 'ap-southeast-1',vcpu: 4, ram: 16, disk: 120 },
] as const;

const fleet = FLEET.map((vm) => ({
  id: vm.id,
  name: vm.name,
  status: vm.status,
  internalIp: vm.ip,
  zone: vm.zone,
  machineType: `${vm.vcpu}-${vm.ram}`,
  vcpu: vm.vcpu,
  ramGb: vm.ram,
  diskGb: vm.disk,
  createdAt: faker.date.past().toISOString(),
}));

// Lookup table so getVmHandler resolves a hand-curated id to the fleet
// entry (kubb's createVmDetail emits a fresh fake id every call — fine
// for the smoke test, awkward for the VM detail page which URLs to a
// specific id from the list).
const fleetById: Map<string, (typeof fleet)[number]> = new Map(
  fleet.map((vm) => [vm.id, vm]),
);

// Hand-curated quota usage so the dashboard widget renders real metric
// names with `used < limit` (kubb's faker returns random strings +
// arbitrary ints, which renders as noise). `metric` keys align with
// `QuotaDefinitionSummary.metric` from the openapi contract.
const QUOTA_USAGE = [
  { metric: 'vm.count',         used: 10, limit: 50 },
  { metric: 'vm.cpu_cores',     used: 38, limit: 128 },
  { metric: 'vm.memory_gb',     used: 172, limit: 512 },
  { metric: 'vm.disk_gb',       used: 1340, limit: 4096 },
  { metric: 'network.fip',      used: 3, limit: 16 },
  { metric: 'storage.buckets',  used: 5, limit: 25 },
] as const;

const quotaUsage = QUOTA_USAGE.map((q, idx) => ({
  definitionId: `quota-def-${idx + 1}`,
  metric: q.metric,
  used: q.used,
  limit: q.limit,
  updatedAt: faker.date.recent({ days: 1 }).toISOString(),
}));

// Hand-curated audit timeline so the dashboard's recent-events list
// shows real dot.case action verbs + target kinds. Timestamps are
// descending (most-recent first) so the widget can render in order.
const ORG_ID = '00000000-0000-0000-0000-000000000001';
const ACTOR = '00000000-0000-0000-0000-0000000000aa';
const now = Date.now();
const auditEvents = [
  { action: 'vm.lifecycle.started',  targetKind: 'vm',   targetId: 'vm-a8c91f2e', ageMin: 3  },
  { action: 'vm.lifecycle.stopped',  targetKind: 'vm',   targetId: 'vm-1b9e4f73', ageMin: 12 },
  { action: 'vm.lifecycle.failed',  targetKind: 'vm',   targetId: 'vm-6f8d22b8', ageMin: 27 },
  { action: 'vm.provisioned',        targetKind: 'vm',   targetId: 'vm-4d2a89e1', ageMin: 45 },
  { action: 'quotas.assignment.changed', targetKind: 'quota', targetId: 'quota-def-1', ageMin: 90 },
  { action: 'node.joined',           targetKind: 'node', targetId: 'node-prod-eu-1-c', ageMin: 180 },
  { action: 'node.draining',         targetKind: 'node', targetId: 'node-prod-eu-1-d', ageMin: 360 },
  { action: 'branding.theme.activated', targetKind: 'theme', targetId: 'plexor-noir', ageMin: 720 },
  { action: 'auth.session.signed_in',targetKind: 'session', targetId: null, ageMin: 24 * 60 },
  { action: 'auth.api_key.created',  targetKind: 'api_key', targetId: 'apk-cicd-deployer', ageMin: 30 * 60 },
] as const;

const auditTimeline = auditEvents.map((e, idx) => ({
  id: `audit-${idx + 1}`,
  action: e.action,
  orgId: ORG_ID,
  actorUserId: ACTOR,
  targetKind: e.targetKind,
  targetId: e.targetId,
  payload: {},
  occurredAt: new Date(now - e.ageMin * 60_000).toISOString(),
}));

export const handlers: RequestHandler[] = [
  // ───────────────────────── VMs (6) ─────────────────────────
  listVmsHandler(createVmList({ items: fleet, total: fleet.length, page: 1, pageSize: 20 })),
  getVmHandler((info) => {
    const id = String((info.params as { vmId: unknown }).vmId);
    const vm = fleetById.get(id);
    if (!vm) {
      return new Response(JSON.stringify({ status: 404, title: 'Not Found' }), {
        status: 404,
        headers: { 'Content-Type': 'application/problem+json' },
      });
    }
    return new Response(JSON.stringify(createVmDetail({ ...vm })), {
      status: 200,
      headers: { 'Content-Type': 'application/json' },
    });
  }),
  provisionVmHandler(createVmDetail()),
  startVmHandler(createVmDetail()),
  stopVmHandler(createVmDetail()),
  deleteVmHandler(),

  // ───────────────────────── Nodes (4) ─────────────────────────
  nodeJoinHandler(createNodeJoinResponse()),
  nodeHeartbeatHandler(createNodeHeartbeat200()),
  nodeCommandPollHandler(createNodeCommandPollResponse({ commands: [], nextCursor: 0 })),
  nodeCommandResultHandler(createNodeCommandResult()),

  // ───────────────────────── Quotas (5) ─────────────────────────
  getQuotaDefinitionsHandler(
    faker.helpers.multiple(() => createQuotaDefinitionSummary(), { count: 6 }),
  ),
  listQuotaAssignmentsHandler(
    faker.helpers.multiple(() => createQuotaAssignmentSummary(), { count: 8 }),
  ),
  upsertQuotaAssignmentHandler(createQuotaAssignmentSummary()),
  deleteQuotaAssignmentHandler(),
  listQuotaUsageHandler(quotaUsage),
  listEffectiveQuotasHandler(
    faker.helpers.multiple(() => createEffectiveQuotaEntry(), { count: 6 }),
  ),

  // ───────────────────────── Branding (9) ─────────────────────────
  getBrandingGlobalHandler(createGlobalThemeConfigResponse()),
  updateBrandingGlobalHandler(createGlobalThemeConfigResponse()),
  getBrandingBootHandler(createBrandingBootConfig()),
  getBrandingOrgHandler(createOrgBrandingConfigResponse()),
  updateBrandingOrgHandler(createOrgBrandingConfigResponse()),
  deleteBrandingOrgHandler(),
  getBrandingThemeHandler(createGetBrandingTheme200()),
  updateBrandingThemeHandler(createGetBrandingTheme200()),
  deleteBrandingThemeHandler(),

  // ───────────────────────── Audit (1) ─────────────────────────
  getAuditHandler(auditTimeline),

  // ───────────────────────── Auth providers (3) ─────────────────────────
  getOrgAuthProviderHandler(createOrgAuthProviderConfigResponse()),
  updateOrgAuthProviderHandler(createOrgAuthProviderConfigResponse()),
  testOrgAuthProviderHandler(createOrgAuthProviderTestResult({ ok: true })),

  // ───────────────────────── OIDC (3) ─────────────────────────
  //
  // The OIDC endpoints return 302 in the OpenAPI contract; kubb's
  // generator emits 200 by default. The dev:mock worker never reaches
  // these — the FE uses `window.location.assign` to navigate, which
  // bypasses the service worker — so wiring them with a static 200
  // body is enough to keep an accidental `fetch('/auth/oidc/...')`
  // call from crashing. A real browser-driven flow would need a
  // 302; mark these `x-passthrough: true` in the contract when the
  // backend lands.
  getOidcAuthorizeHandler({ redirectUrl: 'https://mock-idp.example.com/authorize' }),
  getOidcCallbackHandler({ status: 'ok' }),
  postOidcLogoutHandler(),
];
