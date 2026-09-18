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
  // Login (1 — kubb skipped this in the MSW pass; see kz note in
  // msw/postAuthLoginHandler.ts. Returns 200 + kubb-generated faker
  // fixture so the FE's postAuthLogin() always resolves with a
  // mock-shaped body in dev:mock mode.)
  postAuthLoginHandler,
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
  createQuotaUsageEntry,
  createEffectiveQuotaEntry,
  createGlobalThemeConfigResponse,
  createBrandingBootConfig,
  createOrgBrandingConfigResponse,
  createGetBrandingTheme200,
  createAuditQueryResponse,
  createOrgAuthProviderConfigResponse,
  createOrgAuthProviderTestResult,
  createPostAuthLogin200,
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
  listQuotaUsageHandler(
    faker.helpers.multiple(() => createQuotaUsageEntry(), { count: 6 }),
  ),
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
  getAuditHandler(
    faker.helpers.multiple(() => createAuditQueryResponse(), { count: 12 }),
  ),

  // ───────────────────────── Auth providers (3) ─────────────────────────
  getOrgAuthProviderHandler(createOrgAuthProviderConfigResponse()),
  updateOrgAuthProviderHandler(createOrgAuthProviderConfigResponse()),
  testOrgAuthProviderHandler(createOrgAuthProviderTestResult({ ok: true })),

  // ───────────────────────── Login (1) ─────────────────────────
  //
  // Phase 4.6 endpoint — kubb skipped it in the MSW + faker pass
  // (kz note in msw/postAuthLoginHandler.ts). 200 with a hand-crafted
  // token + user triple so postAuthLogin() in dev:mock mode always
  // resolves with a mock-shaped body. Tokens come from faker so
  // each reload is fresh; matches the dev:mock session-bearer contract.
  postAuthLoginHandler(createPostAuthLogin200()),

  // ───────────────────────── OIDC (3) ─────────────────────────
  //
  // The OIDC endpoints return 302 in the OpenAPI contract; kubb's
  // generator emits 200 by default. The dev:mock worker never reaches
  // these — the FE uses `window.location.assign` to navigate, which
  // bypasses the service worker — so wiring them with a static 200
  // body (and a `redirectUrl` field so the FE has something to read if
  // it ever does call them) is enough to keep an accidental
  // `fetch('/auth/oidc/...')` from crashing. A real browser-driven flow
  // would need a 302; mark these `x-passthrough: true` in the contract
  // when the backend lands.
  getOidcAuthorizeHandler({ redirectUrl: 'https://mock-idp.example.com/authorize' }),
  getOidcCallbackHandler({ status: 'ok', redirectUrl: '/?access_token=mock-idp-callback-token' }),
  postOidcLogoutHandler(),
];
