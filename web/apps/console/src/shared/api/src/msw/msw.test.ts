/**
 * MSW smoke test — boots the same handlers that `mocks/browser.ts` wires
 * into the dev worker (kubb-generated per-operation factories fed with
 * faker fixtures + 3 hand-mirrored /branding/theme handlers) under the
 * Node MSW server (msw/node → setupServer), and exercises the admin-page
 * mount surface end-to-end.
 *
 * Why this exists:
 *   - Catches missing handlers before they reach the browser (the dev:mock
 *     worker happily logs "unhandled request" and the SPA crashes).
 *   - Catches handler/fixture drift after a kubb regen — a deleted
 *     factory or a renamed export breaks the worker before the user
 *     notices.
 *   - Documents the "UI hits these endpoints on mount" contract — add a
 *     line here when you add an admin page; the assertion is the
 *     canary that the new endpoint is wired.
 *
 * What this is NOT:
 *   - A unit test of any specific handler's body. The kubb factories
 *     emit bodies via the fixture functions; verifying a specific
 *     field value would re-test faker's `arrayElement`, not our wiring.
 */
import { afterAll, afterEach, beforeAll, describe, expect, it } from 'vitest';
import { setupServer } from 'msw/node';
import { handlers } from '../../mocks/handlers';

const server = setupServer(...handlers);

beforeAll(() => server.listen({ onUnhandledRequest: 'error' }));
afterEach(() => server.resetHandlers());
afterAll(() => server.close());

describe('MSW handler coverage — admin-page mount surface', () => {
  it('covers /api/v1/vms (VMs list page)', async () => {
    const res = await fetch('/vms');
    expect(res.status).toBe(200);
    const body = await res.json();
    expect(body).toHaveProperty('items');
    expect(Array.isArray(body.items)).toBe(true);
    expect(body.items.length).toBeGreaterThan(0);
  });

  it('covers /api/v1/vms/:vmId (VM detail page)', async () => {
    const list = await fetch('/vms').then((r) => r.json());
    const id = list.items[0].id as string;
    const res = await fetch(`/vms/${id}`);
    expect(res.status).toBe(200);
    const body = await res.json();
    expect(body.id).toBe(id);
  });

  it('covers /api/v1/branding/global (admin branding page)', async () => {
    const res = await fetch('/branding/global');
    expect(res.status).toBe(200);
    const body = await res.json();
    expect(body).toHaveProperty('brandName');
  });

  it('covers /api/v1/branding/boot (boot config — applyBootPreset / applyBootCommunityTheme)', async () => {
    const res = await fetch('/branding/boot');
    expect(res.status).toBe(200);
    const body = await res.json();
    expect(body).toHaveProperty('brandName');
    expect(body).toHaveProperty('defaultPresetId');
  });

  it('covers /api/v1/branding/org/:orgId (admin org override)', async () => {
    const res = await fetch('/branding/org/00000000-0000-0000-0000-000000000001');
    expect(res.status).toBe(200);
  });

  it('covers /api/v1/branding/theme (hand-mirrored — kubb skipped this)', async () => {
    const res = await fetch('/branding/theme');
    expect(res.status).toBe(200);
    const body = await res.json();
    expect(body).toHaveProperty('themeId');
  });

  it('covers /api/v1/audit (audit timeline)', async () => {
    const res = await fetch('/audit');
    expect(res.status).toBe(200);
    const body = await res.json();
    expect(Array.isArray(body)).toBe(true);
    expect(body.length).toBeGreaterThan(0);
  });

  it('covers /api/v1/quotas/definitions', async () => {
    const res = await fetch('/quotas/definitions');
    expect(res.status).toBe(200);
    const body = await res.json();
    expect(Array.isArray(body)).toBe(true);
  });

  it('covers /api/v1/quotas/usage', async () => {
    const res = await fetch('/quotas/usage');
    expect(res.status).toBe(200);
    const body = await res.json();
    expect(Array.isArray(body)).toBe(true);
  });

  it('covers /api/v1/quotas/effective', async () => {
    const res = await fetch('/quotas/effective');
    expect(res.status).toBe(200);
    const body = await res.json();
    expect(Array.isArray(body)).toBe(true);
  });

  it('covers /api/v1/iam/orgs/:orgId/auth-provider (admin auth page)', async () => {
    const res = await fetch('/iam/orgs/00000000-0000-0000-0000-000000000001/auth-provider');
    expect(res.status).toBe(200);
    const body = await res.json();
    expect(body).toHaveProperty('provider');
  });

  it('covers /api/v1/iam/orgs/:orgId/auth-provider/test', async () => {
    const res = await fetch('/iam/orgs/00000000-0000-0000-0000-000000000001/auth-provider/test', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ provider: 'local' }),
    });
    expect(res.status).toBe(200);
    const body = await res.json();
    expect(body).toHaveProperty('ok');
  });

  it('covers VM lifecycle endpoints (start/stop/delete)', async () => {
    const list = await fetch('/vms').then((r) => r.json());
    const id = list.items[0].id as string;
    expect((await fetch(`/vms/${id}/start`, { method: 'POST' })).status).toBe(200);
    expect((await fetch(`/vms/${id}/stop`, { method: 'POST' })).status).toBe(200);
    expect((await fetch(`/vms/${id}`, { method: 'DELETE' })).status).toBe(204);
  });

  it('covers PUT /api/v1/branding/theme (hand-mirrored)', async () => {
    const res = await fetch('/branding/theme', {
      method: 'PUT',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ themeId: 'plexor-noir' }),
    });
    expect(res.status).toBe(200);
    const body = await res.json();
    // The kubb-style factory returns the static fixture (faker-chosen
    // themeId) regardless of request body — assert the shape, not the
    // round-tripped themeId. A real backend would echo the input.
    expect(body).toHaveProperty('themeId');
    expect(typeof body.themeId).toBe('string');
  });

  it('covers DELETE /api/v1/branding/theme (hand-mirrored)', async () => {
    const res = await fetch('/branding/theme', { method: 'DELETE' });
    expect(res.status).toBe(204);
  });

  // ───────────────────────── Login (1) ─────────────────────────
  // Phase 4.6 endpoint — kubb skipped this in the MSW + faker pass
  // (kz note in msw/postAuthLoginHandler.ts). The dev:mock worker
  // returns 200 + a kubb-shaped login response so the login page's
  // postAuthLogin() always resolves with a usable mock triple.

  it('covers POST /api/v1/auth/login (login page mount)', async () => {
    const res = await fetch('/auth/login', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ email: 'demo@plexor.test', password: 'mock-password' }),
    });
    expect(res.status).toBe(200);
    const body = await res.json();
    expect(body).toHaveProperty('accessToken');
    expect(body).toHaveProperty('refreshToken');
    expect(body).toHaveProperty('expiresIn');
    expect(body.user).toMatchObject({ email: expect.any(String) });
  });

  // ───────────────────────── OIDC (302 kubb-stubbed as 200) ─────────────────────────
  // The OIDC endpoints return 302 in the OpenAPI contract; kubb's
  // generator emits 200 by default. The dev:mock worker mirrors that
  // with a static 200 body so any accidental `fetch('/auth/oidc/...')`
  // gets a usable body. A real browser-driven flow would need a 302;
  // mark these `x-passthrough: true` in the contract when the backend
  // lands.

  it('covers GET /api/v1/auth/oidc/authorize with a redirect URL', async () => {
    const res = await fetch('/auth/oidc/authorize?org=00000000-0000-0000-0000-000000000001');
    expect(res.status).toBe(200);
    const body = await res.json();
    expect(body).toHaveProperty('redirectUrl');
    expect(typeof body.redirectUrl).toBe('string');
  });

  it('covers GET /api/v1/auth/oidc/callback with a status field', async () => {
    const res = await fetch('/auth/oidc/callback?code=mock-code&state=mock-state');
    expect(res.status).toBe(200);
    const body = await res.json();
    expect(body).toHaveProperty('status');
  });

  // ───────────────────────── Remaining wired handlers ─────────────────────────
  // Every handler wired in mocks/handlers.ts gets at least one smoke line —
  // a handler that only exists in the wiring but is never exercised here is
  // a wiring the dev:mock worker silently relies on.

  it('covers POST /api/v1/vms (provision VM)', async () => {
    const res = await fetch('/vms', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ name: 'smoke-vm', machineType: '2-4' }),
    });
    expect(res.status).toBe(201);
    const body = await res.json();
    expect(body).toHaveProperty('id');
  });

  it('covers POST /api/v1/nodes/join (node registration)', async () => {
    const res = await fetch('/nodes/join', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ joinToken: 'dev', hostname: 'node-smoke.local' }),
    });
    expect(res.status).toBe(201);
    const body = await res.json();
    expect(body).toHaveProperty('nodeId');
  });

  it('covers POST /api/v1/nodes/:nodeId/heartbeat', async () => {
    const res = await fetch('/nodes/00000000-0000-0000-0000-000000000001/heartbeat', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ agentVersion: '0.1.0-dev' }),
    });
    expect(res.status).toBe(200);
  });

  it('covers POST /api/v1/nodes/:nodeId/commands/poll', async () => {
    const res = await fetch('/nodes/00000000-0000-0000-0000-000000000001/commands/poll', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ waitCursor: 0 }),
    });
    expect(res.status).toBe(200);
    const body = await res.json();
    expect(body).toHaveProperty('nextCursor');
  });

  it('covers POST /api/v1/nodes/:nodeId/commands/:commandId/result', async () => {
    const res = await fetch(
      '/nodes/00000000-0000-0000-0000-000000000001/commands/00000000-0000-0000-0000-000000000002/result',
      {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ status: 'ok' }),
      },
    );
    expect(res.status).toBe(200);
  });

  it('covers GET /api/v1/quotas/assignments', async () => {
    const res = await fetch('/quotas/assignments');
    expect(res.status).toBe(200);
    const body = await res.json();
    expect(Array.isArray(body)).toBe(true);
    expect(body.length).toBeGreaterThan(0);
  });

  it('covers PUT + DELETE /api/v1/quotas/assignments', async () => {
    const put = await fetch('/quotas/assignments', {
      method: 'PUT',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ quotaKey: 'vms.per.org', limit: 10 }),
    });
    expect(put.status).toBe(200);
    const body = await put.json();
    expect(body).toHaveProperty('definitionId');
    expect(body).toHaveProperty('scope');

    const del = await fetch('/quotas/assignments/00000000-0000-0000-0000-000000000003', {
      method: 'DELETE',
    });
    expect(del.status).toBe(204);
  });

  it('covers PUT /api/v1/branding/global (update global theme)', async () => {
    const res = await fetch('/branding/global', {
      method: 'PUT',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ brandName: 'Plexor' }),
    });
    expect(res.status).toBe(200);
    const body = await res.json();
    expect(body).toHaveProperty('brandName');
  });

  it('covers PUT + DELETE /api/v1/branding/org/:orgId (org override lifecycle)', async () => {
    const put = await fetch('/branding/org/00000000-0000-0000-0000-000000000001', {
      method: 'PUT',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ brandName: 'Acme' }),
    });
    expect(put.status).toBe(200);

    const del = await fetch('/branding/org/00000000-0000-0000-0000-000000000001', {
      method: 'DELETE',
    });
    expect(del.status).toBe(204);
  });

  it('covers PUT /api/v1/iam/orgs/:orgId/auth-provider (update config)', async () => {
    const res = await fetch('/iam/orgs/00000000-0000-0000-0000-000000000001/auth-provider', {
      method: 'PUT',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ provider: 'sigil' }),
    });
    expect(res.status).toBe(200);
    const body = await res.json();
    expect(body).toHaveProperty('provider');
  });

  it('covers POST /api/v1/auth/oidc/logout', async () => {
    const res = await fetch('/auth/oidc/logout', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({}),
    });
    expect(res.status).toBe(204);
  });
});
