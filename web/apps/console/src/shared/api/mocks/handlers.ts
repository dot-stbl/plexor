// MSW request handlers — composed from kubb-generated per-operation factories,
// fed with kubb-generated faker fixtures.
//
// Coverage:
//   32 handlers come from `bun run generate` (kubb); see
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
//
// Mock data for endpoints NOT yet in the contract lives in
// `shared/api/mocks/handmade/` (each with a TODO(contract) header) — never
// inline in features.
//
// Shared hand-curated data (the VM fleet, cluster set, audit entries) lives
// in `web/apps/console/src/mocks/` so component tests AND this handler file
// read from the same source. Don't inline fixtures here — see mocks/README.md.
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
  // Auth (1 — kubb-generated from /auth/login, Phase 4.6 sigil endpoint)
  postAuthLoginHandler,
  // vSphere (3 — issue #77, alternate compute provider)
  getVSphereInventoryHandler,
  refreshVSphereInventoryHandler,
  cloneVSphereTemplateHandler,
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
  createOrgAuthProviderConfigResponse,
  createOrgAuthProviderTestResult,
  createPostAuthLogin200,
  createVSphereInventoryResponse,
  createVSphereInventoryClusterRow,
  createVSphereInventoryHostRow,
  createVSphereInventoryVirtualMachineRow,
  createVSphereInventoryRefreshResponse,
  createVSphereCloneResponse,
} from '@/shared/api';
import { DEV_SESSION, resetMockRng } from '@/mocks';
import { getMockScenario } from '@/mocks/db/scenario';
import { mockDelay } from '@/mocks/db/latency';
import {
  listVms,
  getVm as getStoreVm,
  getVmDetailExtras,
  createVm,
  startVm,
  stopVm,
  deleteVm,
} from '@/mocks/db/store';
import { queryAuditEntries } from '@/mocks/audit/audit';
import type { CreateVmRequest, ProblemDetails } from '@/shared/api';

// Deterministic mocks — same data every reload (stable UI + screenshots).
// Seeded by the shared mocks/ utility so the fleet + audit counts stay in
// sync with the launcher SUMMARY card.
resetMockRng();

// ───────────────────────── vSphere fixture data (issue #77) ─────────────────────────
//
// Hand-curated inventory so the list renders a realistic mix of
// clusters / hosts / VMs. The snapshot id is stable across reloads so
// the inventory list + the refresh-handler response line up.
const VSPHERE_SNAPSHOT_ID = '01928374-aaa0-7000-8000-000000000001';
const VSPHERE_VCENTER_MOREF = 'primary';
const VSPHERE_DATACENTER_MOREF = 'datacenter-2';
const VSPHERE_INVENTORY_FIXTURE = {
  snapshot: {
    id: VSPHERE_SNAPSHOT_ID,
    vcenterMoref: VSPHERE_VCENTER_MOREF,
    datacenterCount: 1,
    clusterCount: 2,
    hostCount: 3,
    virtualMachineCount: 4,
    refreshedAt: '2026-09-21T08:00:00Z',
  },
  clusters: [
    createVSphereInventoryClusterRow({
      moref: 'domain-c7',
      name: 'cluster-prod-01',
      datacenterMoref: VSPHERE_DATACENTER_MOREF,
      drsEnabled: true,
    }),
    createVSphereInventoryClusterRow({
      moref: 'domain-c8',
      name: 'cluster-staging-01',
      datacenterMoref: VSPHERE_DATACENTER_MOREF,
      drsEnabled: false,
    }),
  ],
  hosts: [
    createVSphereInventoryHostRow({
      moref: 'host-21',
      name: 'esxi-01.corp.example.com',
      clusterMoref: 'domain-c7',
      connectionState: 'CONNECTED',
      cpuCores: 32,
      memoryMib: 262144,
    }),
    createVSphereInventoryHostRow({
      moref: 'host-22',
      name: 'esxi-02.corp.example.com',
      clusterMoref: 'domain-c7',
      connectionState: 'CONNECTED',
      cpuCores: 32,
      memoryMib: 262144,
    }),
    createVSphereInventoryHostRow({
      moref: 'host-23',
      name: 'esxi-staging-01.corp.example.com',
      clusterMoref: 'domain-c8',
      connectionState: 'DISCONNECTED',
      cpuCores: 16,
      memoryMib: 131072,
    }),
  ],
  virtualMachines: [
    createVSphereInventoryVirtualMachineRow({
      moref: 'vm-1234',
      name: 'web-prod-01',
      folderPath: '/Datacenter/vm/Tenants/Acme',
      powerState: 'POWERED_ON',
      cpuCount: 4,
      memoryMib: 16384,
      hostMoref: 'host-21',
    }),
    createVSphereInventoryVirtualMachineRow({
      moref: 'vm-1235',
      name: 'db-prod-01',
      folderPath: '/Datacenter/vm/Tenants/Acme',
      powerState: 'POWERED_ON',
      cpuCount: 8,
      memoryMib: 32768,
      hostMoref: 'host-21',
    }),
    createVSphereInventoryVirtualMachineRow({
      moref: 'vm-1236',
      name: 'ubuntu-22.04-base',
      folderPath: '/Datacenter/vm/Templates',
      powerState: 'POWERED_OFF',
      cpuCount: 2,
      memoryMib: 4096,
      hostMoref: 'host-22',
    }),
    createVSphereInventoryVirtualMachineRow({
      moref: 'vm-1237',
      name: 'staging-api',
      folderPath: '/Datacenter/vm/Tenants/Acme/Staging',
      powerState: 'SUSPENDED',
      cpuCount: 2,
      memoryMib: 8192,
      hostMoref: 'host-23',
    }),
  ],
};

export const handlers: RequestHandler[] = [
  // ───────────────────────── VMs (6) ─────────────────────────
  listVmsHandler(async () => {
    await mockDelay(200);
    const scenario = getMockScenario();
    if (scenario === 'error') {
      return new Response(
        JSON.stringify({ status: 500, title: 'Internal Server Error', detail: 'Mock scenario: error' } satisfies ProblemDetails),
        { status: 500, headers: { 'Content-Type': 'application/problem+json' } },
      );
    }
    const items = scenario === 'empty' ? [] : listVms();
    return new Response(
      JSON.stringify(createVmList({ items, total: items.length, page: 1, pageSize: 20 })),
      { status: 200, headers: { 'Content-Type': 'application/json' } },
    );
  }),
  getVmHandler(async (info) => {
    await mockDelay(150);
    const id = String((info.params as { vmId: unknown }).vmId);
    const vm = getStoreVm(id);
    if (!vm) {
      return new Response(JSON.stringify({ status: 404, title: 'Not Found' } satisfies ProblemDetails), {
        status: 404,
        headers: { 'Content-Type': 'application/problem+json' },
      });
    }
    const extras = getVmDetailExtras(id);
    return new Response(JSON.stringify(createVmDetail({ ...vm, ...extras })), {
      status: 200,
      headers: { 'Content-Type': 'application/json' },
    });
  }),
  provisionVmHandler(async (info) => {
    const scenario = getMockScenario();
    if (scenario === 'error') {
      return new Response(
        JSON.stringify({ status: 500, title: 'Internal Server Error', detail: 'Mock scenario: error' } satisfies ProblemDetails),
        { status: 500, headers: { 'Content-Type': 'application/problem+json' } },
      );
    }
    await mockDelay(300);
    const body = (await info.request.json().catch(() => ({}))) as Partial<CreateVmRequest>;
    const name = body.name?.trim() ?? '';
    if (!name) {
      return new Response(
        JSON.stringify({
          type: 'https://plexor.dev/problems/vms/name-required',
          title: 'Validation failed',
          status: 422,
          detail: 'name is required.',
        } satisfies ProblemDetails),
        { status: 422, headers: { 'Content-Type': 'application/problem+json' } },
      );
    }
    if (!/^[a-z0-9]([a-z0-9-]{0,61}[a-z0-9])?$/.test(name)) {
      return new Response(
        JSON.stringify({
          type: 'https://plexor.dev/problems/vms/name-invalid',
          title: 'Validation failed',
          status: 422,
          detail: 'Name must be lowercase letters, digits and hyphens.',
        } satisfies ProblemDetails),
        { status: 422, headers: { 'Content-Type': 'application/problem+json' } },
      );
    }
    const duplicate = listVms().some((vm) => vm.name.toLowerCase() === name.toLowerCase());
    if (duplicate) {
      return new Response(
        JSON.stringify({
          type: 'https://plexor.dev/problems/vms/name-conflict',
          title: 'Conflict',
          status: 409,
          detail: `A VM named '${name}' already exists.`,
        } satisfies ProblemDetails),
        { status: 409, headers: { 'Content-Type': 'application/problem+json' } },
      );
    }
    const vm = createVm({ ...body, name }, DEV_SESSION.user.id);
    const extras = getVmDetailExtras(vm.id);
    return new Response(JSON.stringify(createVmDetail({ ...vm, ...extras })), {
      status: 201,
      headers: { 'Content-Type': 'application/json' },
    });
  }),
  startVmHandler(async (info) => {
    await mockDelay(200);
    const id = String((info.params as { vmId: unknown }).vmId);
    const vm = startVm(id, DEV_SESSION.user.id);
    if (!vm) {
      return new Response(JSON.stringify({ status: 404, title: 'Not Found' } satisfies ProblemDetails), {
        status: 404,
        headers: { 'Content-Type': 'application/problem+json' },
      });
    }
    const extras = getVmDetailExtras(id);
    return new Response(JSON.stringify(createVmDetail({ ...vm, ...extras })), {
      status: 200,
      headers: { 'Content-Type': 'application/json' },
    });
  }),
  stopVmHandler(async (info) => {
    await mockDelay(200);
    const id = String((info.params as { vmId: unknown }).vmId);
    const vm = stopVm(id, DEV_SESSION.user.id);
    if (!vm) {
      return new Response(JSON.stringify({ status: 404, title: 'Not Found' } satisfies ProblemDetails), {
        status: 404,
        headers: { 'Content-Type': 'application/problem+json' },
      });
    }
    const extras = getVmDetailExtras(id);
    return new Response(JSON.stringify(createVmDetail({ ...vm, ...extras })), {
      status: 200,
      headers: { 'Content-Type': 'application/json' },
    });
  }),
  deleteVmHandler(async (info) => {
    await mockDelay(150);
    const id = String((info.params as { vmId: unknown }).vmId);
    deleteVm(id, DEV_SESSION.user.id);
    return new Response(null, { status: 204 });
  }),

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
  getAuditHandler(async (info) => {
    await mockDelay(150);
    const scenario = getMockScenario();
    if (scenario === 'error') {
      return new Response(
        JSON.stringify({ status: 500, title: 'Internal Server Error' } satisfies ProblemDetails),
        { status: 500, headers: { 'Content-Type': 'application/problem+json' } },
      );
    }
    if (scenario === 'empty') {
      return new Response(JSON.stringify([]), { status: 200, headers: { 'Content-Type': 'application/json' } });
    }
    const url = new URL(info.request.url);
    const limitParam = url.searchParams.get('limit');
    const rows = queryAuditEntries({
      action: url.searchParams.get('action') ?? undefined,
      actorUserId: url.searchParams.get('actorUserId') ?? undefined,
      since: url.searchParams.get('since') ?? undefined,
      before: url.searchParams.get('before') ?? undefined,
      limit: limitParam ? Number(limitParam) : undefined,
    });
    return new Response(JSON.stringify(rows), { status: 200, headers: { 'Content-Type': 'application/json' } });
  }),

  // ───────────────────────── Auth providers (3) ─────────────────────────
  getOrgAuthProviderHandler(createOrgAuthProviderConfigResponse()),
  updateOrgAuthProviderHandler(createOrgAuthProviderConfigResponse()),
  testOrgAuthProviderHandler(createOrgAuthProviderTestResult({ ok: true })),

  // ───────────────────────── Login (1) ─────────────────────────
  //
  // Phase 4.6 endpoint — generated by kubb from /auth/login. 200 with a
  // faker-shaped token + user triple so postAuthLogin() in dev:mock mode
  // always resolves with a mock-shaped body. Tokens come from faker so
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

  // ───────────────────────── vSphere (3 — issue #77) ─────────────────────────
  //
  // Hand-curated inventory so the list renders a realistic mix of
  // clusters / hosts / VMs. The refresh handler increments the
  // snapshot id + refreshes `RefreshedAt`; the clone handler echoes a
  // deterministic new VM mo-ref so the success toast is stable.
  getVSphereInventoryHandler(
    createVSphereInventoryResponse(VSPHERE_INVENTORY_FIXTURE),
  ),
  refreshVSphereInventoryHandler(
    createVSphereInventoryRefreshResponse({
      snapshotId: VSPHERE_SNAPSHOT_ID,
      status: 'SUCCESS',
    }),
  ),
  cloneVSphereTemplateHandler(
    createVSphereCloneResponse({
      runId: '01928374-bbb0-7000-8000-000000000001',
      vmMoref: 'vm-9001',
      status: 'SUCCESS',
    }),
  ),
];
