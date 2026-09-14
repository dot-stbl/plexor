/**
 * BrandingService — typed wrapper around the backend's
 * /api/v1/branding/* endpoints. Hand-rolled because the kubb code-gen
 * pipeline generates the VM/cluster surfaces only; the branding
 * endpoints are a brand-new addition. Migration path: replace each
 * function with the kubb-generated client + useQuery once the FE
 * codegen step is run against the new OpenAPI spec (the spec was
 * regenerated with the branding endpoints in commit 2).
 *
 * The runtime base URL is read from `import.meta.env.VITE_API_BASE_URL`;
 * when unset (dev with mocks) the calls fail gracefully — the MSW
 * handlers under `src/mocks` short-circuit the network.
 */
import type {
  GlobalBrandingConfig,
  OrgBrandingConfig,
  ResolvedBootBranding,
  UpsertGlobalBrandingRequest,
  UpsertOrgBrandingRequest,
} from './branding-types';

/** Read once at module load. Falls back to relative path (proxy)
 * so the same code works in dev (Vite proxy) + production (reverse
 * proxy / k8s ingress). */
const baseUrl = (() => {
  const explicit = import.meta.env.VITE_API_BASE_URL;
  return (explicit ?? '').replace(/\/$/, '');
})();

/** Build the full URL for a branding endpoint. */
function url(path: string): string {
  return `${baseUrl}/api/v1/branding${path}`;
}

/** Throws on non-2xx so TanStack Query surfaces the error to the
 * UI. The body is best-effort JSON; non-JSON errors fall through
 * with a generic message.
 */
async function request<T>(
  method: 'GET' | 'POST' | 'PUT' | 'DELETE',
  path: string,
  body?: unknown,
): Promise<T> {
  const init: RequestInit = {
    method,
    credentials: 'include',
    headers: body !== undefined ? { 'content-type': 'application/json' } : {},
  };
  if (body !== undefined) {
    init.body = JSON.stringify(body);
  }

  const response = await fetch(url(path), init);

  if (!response.ok) {
    let detail: string;
    try {
      const problem = (await response.json()) as { detail?: string; title?: string };
      detail = problem.detail ?? problem.title ?? response.statusText;
    } catch {
      detail = response.statusText;
    }
    throw new Error(`Branding ${method} ${path} failed (${response.status}): ${detail}`);
  }

  if (response.status === 204) {
    return undefined as T;
  }
  return (await response.json()) as T;
}

/**
 * GET /api/v1/branding/global — read the operator-global branding row.
 * The seeder inserts a default row on first boot; the API returns
 * 200 with the defaults even before the seeder runs (defensive).
 */
export function getGlobalBranding(): Promise<GlobalBrandingConfig> {
  return request<GlobalBrandingConfig>('GET', '/global');
}

/**
 * PUT /api/v1/branding/global — upsert the operator-global row.
 * `branding.update` permission required (admin-only).
 */
export function updateGlobalBranding(
  payload: UpsertGlobalBrandingRequest,
): Promise<GlobalBrandingConfig> {
  return request<GlobalBrandingConfig>('PUT', '/global', payload);
}

/**
 * GET /api/v1/branding/org/{orgId} — read the per-org override row.
 * Returns null when no override exists (404 from backend).
 */
export function getOrgBranding(orgId: string): Promise<OrgBrandingConfig | null> {
  return request<OrgBrandingConfig | null>(
    'GET',
    `/org/${encodeURIComponent(orgId)}`,
  ).catch((error: Error) => {
    if (error.message.includes('(404)')) {
      return null;
    }
    throw error;
  });
}

/**
 * PUT /api/v1/branding/org/{orgId} — upsert the per-org override row.
 * Null fields mean "inherit the operator global default".
 */
export function updateOrgBranding(
  orgId: string,
  payload: UpsertOrgBrandingRequest,
): Promise<OrgBrandingConfig> {
  return request<OrgBrandingConfig>(
    'PUT',
    `/org/${encodeURIComponent(orgId)}`,
    payload,
  );
}

/**
 * DELETE /api/v1/branding/org/{orgId} — reset the per-org override
 * (the tenant reverts to the operator defaults). Idempotent — a
 * missing row is a no-op.
 */
export function deleteOrgBranding(orgId: string): Promise<void> {
  return request<void>('DELETE', `/org/${encodeURIComponent(orgId)}`);
}

/**
 * GET /api/v1/branding/boot — resolved boot config the FE boot
 * script writes to `window.__PLEXOR_CONFIG__`. The backend merges
 * the operator-global row + per-org override for the caller's
 * tenant; null fields fall back to the global default.
 */
export function getBootBranding(): Promise<ResolvedBootBranding> {
  return request<ResolvedBootBranding>('GET', '/boot');
}