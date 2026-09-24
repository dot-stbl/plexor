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
    await mockDelay(300);
    const body = (await info.request.json().catch(() => ({}))) as Partial<CreateVmRequest>;
    if (!body.name || !body.name.trim()) {
      return new Response(
        JSON.stringify({
          type: 'https://plexor.dev/problems/validation',
          title: 'Validation failed',
          status: 422,
          detail: 'name is required.',
        } satisfies ProblemDetails),
        { status: 422, headers: { 'Content-Type': 'application/problem+json' } },
      );
    }
    const vm = createVm(body, DEV_SESSION.user.id);
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
];
