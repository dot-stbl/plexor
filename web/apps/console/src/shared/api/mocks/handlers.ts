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
} from '@/shared/api';
// Login (1) — POST /auth/login is not in the contract yet; the handler +
// fixture are handmade. Returns 200 + a faker-shaped login response so the
// FE's postAuthLogin() always resolves with a mock-shaped body in dev:mock.
import { createPostAuthLogin200, postAuthLoginHandler } from './handmade/post-auth-login';
import { FLEET, FLEET_BY_ID, resetMockRng } from '@/mocks';

// Deterministic mocks — same data every reload (stable UI + screenshots).
// Seeded by the shared mocks/ utility so the fleet + audit counts stay in
// sync with the launcher SUMMARY card.
resetMockRng();

export const handlers: RequestHandler[] = [
  // ───────────────────────── VMs (6) ─────────────────────────
  listVmsHandler(createVmList({ items: FLEET.slice(), total: FLEET.length, page: 1, pageSize: 20 })),
  getVmHandler((info) => {
    const id = String((info.params as { vmId: unknown }).vmId);
    const vm = FLEET_BY_ID.get(id);
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
  // Phase 4.6 endpoint — not in the contract yet, so the handler is
  // handmade (handmade/post-auth-login.ts). 200 with a hand-crafted
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
