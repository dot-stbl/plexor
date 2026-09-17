/**
 * useAuthProviders stub — feature surface for the per-org auth-provider config.
 * Until the kubb-generated query + mutation hooks ship (depends on
 * `/api/v1/iam/orgs/{orgId}/auth-provider`), this is a thin pass-through to the
 * mock service. Callers that need a TanStack-Query shape can wrap it.
 *
 * Re-exports the types for convenience so callers only need one import:
 *   import { useAuthProviders, type OrgAuthProviderConfig } from '@/features/auth-providers/use-auth-providers';
 */
import {
  getOrgAuthProviderConfig,
  updateOrgAuthProviderConfig,
  testOIDCConnection,
} from './auth-providers-service';
import type { OrgAuthProviderConfig } from './auth-providers-types';

export type { OrgAuthProvider, OrgAuthProviderConfig } from './auth-providers-types';

export interface AuthProvidersState {
  config: OrgAuthProviderConfig | null;
  isLoading: boolean;
  error: Error | null;
}

/**
 * Read the current per-org auth-provider config. Stub: synchronous, returns
 * the mock config; consumers should handle `null` while the real hook lands.
 */
export function useAuthProviders(orgId: string): AuthProvidersState {
  return {
    config: getOrgAuthProviderConfig(orgId),
    isLoading: false,
    error: null,
  };
}

export { updateOrgAuthProviderConfig, testOIDCConnection };
