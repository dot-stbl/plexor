/**
 * Auth-provider feature types — Sigil (built-in) vs OIDC (Keycloak/Auth0/etc.).
 * Per-org config; the kubb-generated client + hook will replace this stub once
 * the `/api/v1/iam/orgs/{orgId}/auth-provider` endpoints ship. Until then
 * `src/mocks/auth-providers.ts` drives the /settings/auth UI with in-memory state.
 */

export type OrgAuthProvider = 'sigil' | 'oidc';

export interface OrgAuthProviderConfig {
  provider: OrgAuthProvider;
  oidcAuthority: string | null;
  oidcClientId: string | null;
  /** Redacted (••••••••) on read, plain on write. */
  oidcClientSecret: string | null;
  oidcScopes: string[];
}
