/**
 * useBranding hooks — TanStack Query surface for the branding
 * endpoints. Mirrors the existing use-clusters.ts pattern. The
 * caller wires the hooks into the admin UI's load + save lifecycle.
 */
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import {
  deleteOrgBranding,
  getBootBranding,
  getGlobalBranding,
  getOrgBranding,
  updateGlobalBranding,
  updateOrgBranding,
} from './branding-service';
import type {
  UpsertGlobalBrandingRequest,
  UpsertOrgBrandingRequest,
} from './branding-types';

/** Query key factory — keeps the cache invalidation paths honest. */
export const brandingQueryKeys = {
  global: () => ['branding', 'global'] as const,
  org: (orgId: string) => ['branding', 'org', orgId] as const,
  boot: () => ['branding', 'boot'] as const,
};

/** Read the operator-global branding row. */
export function useGlobalBranding() {
  return useQuery({
    queryKey: brandingQueryKeys.global(),
    queryFn: getGlobalBranding,
  });
}

/** Read the per-org override row (returns null when no override exists). */
export function useOrgBranding(orgId: string) {
  return useQuery({
    queryKey: brandingQueryKeys.org(orgId),
    queryFn: () => getOrgBranding(orgId),
    enabled: orgId !== '',
  });
}

/** Read the merged boot config (org + global). */
export function useBootBranding() {
  return useQuery({
    queryKey: brandingQueryKeys.boot(),
    queryFn: getBootBranding,
  });
}

/** Save the operator-global branding row. On success, invalidate
 * the global + boot queries so the next read picks up the change. */
export function useUpdateGlobalBranding() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (payload: UpsertGlobalBrandingRequest) =>
      updateGlobalBranding(payload),
    onSuccess: async () => {
      await Promise.all([
        queryClient.invalidateQueries({ queryKey: brandingQueryKeys.global() }),
        queryClient.invalidateQueries({ queryKey: brandingQueryKeys.boot() }),
      ]);
    },
  });
}

/** Save the per-org override row. On success, invalidate the org +
 * boot queries. */
export function useUpdateOrgBranding(orgId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (payload: UpsertOrgBrandingRequest) =>
      updateOrgBranding(orgId, payload),
    onSuccess: async () => {
      await Promise.all([
        queryClient.invalidateQueries({ queryKey: brandingQueryKeys.org(orgId) }),
        queryClient.invalidateQueries({ queryKey: brandingQueryKeys.boot() }),
      ]);
    },
  });
}

/** Reset the per-org override row (delete). */
export function useDeleteOrgBranding(orgId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: () => deleteOrgBranding(orgId),
    onSuccess: async () => {
      await Promise.all([
        queryClient.invalidateQueries({ queryKey: brandingQueryKeys.org(orgId) }),
        queryClient.invalidateQueries({ queryKey: brandingQueryKeys.boot() }),
      ]);
    },
  });
}