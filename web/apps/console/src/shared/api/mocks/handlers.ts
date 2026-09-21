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
  // vSphere (3 — issue #77)
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
  createAuditQueryResponse,
  createOrgAuthProviderConfigResponse,
  createOrgAuthProviderTestResult,
  createVSphereInventoryResponse,
  createVSphereInventoryClusterRow,
  createVSphereInventoryHostRow,
  createVSphereInventoryVirtualMachineRow,
  createVSphereInventoryRefreshResponse,
  createVSphereCloneResponse,
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
