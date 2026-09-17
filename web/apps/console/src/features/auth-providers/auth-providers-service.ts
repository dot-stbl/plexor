import { mockGetOrgAuthProvider, mockUpdateOrgAuthProvider, mockTestOIDCConnection } from '@/mocks/auth-providers';
import type { OrgAuthProviderConfig } from './auth-providers-types';

/**
 * Auth-provider service stub. Returns the current per-org config and proxies
 * the test-connection handshake through the in-memory mocks. Will be replaced
 * by kubb-generated `useGetOrgAuthProvider` / `useUpdateOrgAuthProvider` once
 * `/api/v1/iam/orgs/{orgId}/auth-provider` lands.
 */
export function getOrgAuthProviderConfig(_orgId: string): OrgAuthProviderConfig {
  return mockGetOrgAuthProvider();
}

export function updateOrgAuthProviderConfig(
  orgId: string,
  args: Omit<OrgAuthProviderConfig, 'oidcClientSecret'> & { oidcClientSecret: string | null },
): OrgAuthProviderConfig {
  return mockUpdateOrgAuthProvider(orgId, args);
}

export async function testOIDCConnection(
  orgId: string,
  args: { authority: string; clientId: string },
): Promise<{ ok: boolean; message: string }> {
  return mockTestOIDCConnection(orgId, args);
}
