// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// Mock handlers for the auth-provider (Sigil / OIDC toggle) API.
// Per-org config — mirrors what plan/auth-providers will return once
// those endpoints ship. Until then these mocks drive the
// /settings/auth UI.
// ==========================================================================

import type { OrgAuthProvider, OrgAuthProviderConfig } from '@/features/auth-providers/use-auth-providers';

const PLEXOR_DEMO_OIDC = 'https://keycloak.plexor-demo.com/realms/plexor-demo';

/** In-memory per-org config. The mock currently assumes a single org
 *  (`org-demo`) — the future kubb-generated hook will accept an
 *  `orgId` arg and look up per tenant. */
let FAKE_CONFIG: OrgAuthProviderConfig = {
  provider: 'sigil',
  oidcAuthority: null,
  oidcClientId: null,
  oidcClientSecret: null,
  oidcScopes: [],
};

/** Get the current auth-provider config. Mirrors
 *  `GET /api/v1/iam/orgs/{orgId}/auth-provider`. */
export function mockGetOrgAuthProvider(): OrgAuthProviderConfig {
  return { ...FAKE_CONFIG };
}

/** Update the auth-provider config. Mirrors
 *  `PUT /api/v1/iam/orgs/{orgId}/auth-provider`. */
export function mockUpdateOrgAuthProvider(
  orgId: string,
  args: Omit<OrgAuthProviderConfig, 'oidcClientSecret'> & { oidcClientSecret: string | null },
): OrgAuthProviderConfig {
  if (args.provider === 'sigil') {
    FAKE_CONFIG = {
      provider: 'sigil' as OrgAuthProvider,
      oidcAuthority: null,
      oidcClientId: null,
      oidcClientSecret: null,
      oidcScopes: [],
    };
  } else {
    FAKE_CONFIG = {
      provider: args.provider,
      oidcAuthority: args.oidcAuthority,
      oidcClientId: args.oidcClientId,
      // Redact client secret in read response (server-side).
      oidcClientSecret: args.oidcClientSecret ? '••••••••' : null,
      oidcScopes: args.oidcScopes,
    };
  }
  return { ...FAKE_CONFIG };
}

/** Pretend to test the OIDC connection. Mirrors
 *  `POST /api/v1/iam/orgs/{orgId}/auth-provider/test`. */
export async function mockTestOIDCConnection(
  orgId: string,
  args: { authority: string; clientId: string },
): Promise<{ ok: boolean; message: string }> {
  await new Promise((r) => setTimeout(r, 500));
  if (!args.authority.startsWith('https://')) {
    return { ok: false, message: 'Authority must be HTTPS' };
  }
  if (args.authority === PLEXOR_DEMO_OIDC) {
    return { ok: true, message: `Connected to ${args.authority} · 8 realms discovered` };
  }
  return { ok: false, message: 'Could not fetch .well-known/openid-configuration' };
}
