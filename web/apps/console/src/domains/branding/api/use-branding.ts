/**
 * useBranding hooks — TanStack Query surface for the branding endpoints,
 * backed by the kubb-generated client + hooks. The kubb regen
 * (web/tooling/codegen) produces both the typed client functions and the
 * useQuery / useMutation wrappers from contracts/plexor.openapi.yaml.
 *
 * These wrappers adapt the kubb hook API to the one the existing
 * admin/branding.tsx page expects:
 *
 *   - reads:    useGlobalBranding(), useOrgBranding(orgId), useBootBranding()
 *   - writes:   mutateAsync(body), mutate(body), isPending
 *
 * kubb's mutation hooks use `mutate({ data: body })` to keep the
 * request envelope explicit; the wrappers here unwrap it so callers
 * can pass the body directly. The kubb queryKey helpers are
 * re-exported under stable names so consumers don't depend on
 * kubb's auto-generated factory names.
 */
import {
  useGetBrandingGlobal,
  useUpdateBrandingGlobal as useUpdateBrandingGlobalRaw,
  useGetBrandingOrg,
  useUpdateBrandingOrg as useUpdateBrandingOrgRaw,
  useDeleteBrandingOrg as useDeleteBrandingOrgRaw,
  useGetBrandingBoot,
  getBrandingGlobalQueryKey,
  getBrandingOrgQueryKey,
  getBrandingBootQueryKey,
} from '@/shared/api';
import { useQueryClient } from '@tanstack/react-query';

/** Query key factory — keeps cache invalidation paths honest. The
 * kubb-generated helpers return the exact same key shape; we expose
 * them under a stable name so consumers don't depend on kubb's
 * auto-generated factory names. */
export const brandingQueryKeys = {
  global: getBrandingGlobalQueryKey,
  org: getBrandingOrgQueryKey,
  boot: getBrandingBootQueryKey,
};

/** Read the operator-global branding row. */
export function useGlobalBranding() {
  return useGetBrandingGlobal();
}

/** Read the per-org override row. Returns `data: undefined` when no
 * override exists — kubb maps 404 to a typed error; the page treats
 * the absence of `data` as "no override" (the form stays at its
 * default empty state). `retry: false` avoids log noise from the
 * expected 404. The kubb hook disables the query itself when the
 * path param is undefined (an empty string disables it too — the
 * hook's `enabled: !!orgId` check matches our intent). */
export function useOrgBranding(orgId: string) {
  return useGetBrandingOrg(orgId === '' ? undefined : orgId, {
    query: {
      retry: false,
    },
  });
}

/** Read the merged boot config (org + global). */
export function useBootBranding() {
  return useGetBrandingBoot();
}

/**
 * Save the operator-global branding row. The returned mutation
 * accepts the request body directly (kubb's `mutate` wraps the body
 * in `{ data: ... }` under the hood; we unwrap it here so callers
 * can pass `useUpdateGlobalBranding().mutateAsync({ brandName, ... })`
 * without the envelope).
 *
 * On success, invalidate the global + boot queries so the next read
 * picks up the change.
 */
export function useUpdateGlobalBranding() {
  const queryClient = useQueryClient();
  const mutation = useUpdateBrandingGlobalRaw({
    mutation: {
      onSuccess: async () => {
        await Promise.all([
          queryClient.invalidateQueries({ queryKey: getBrandingGlobalQueryKey() }),
          queryClient.invalidateQueries({ queryKey: getBrandingBootQueryKey() }),
        ]);
      },
    },
  });
  return {
    ...mutation,
    mutate: (payload: Parameters<typeof mutation.mutate>[0]['data']) =>
      mutation.mutate({ data: payload }),
    mutateAsync: (payload: Parameters<typeof mutation.mutateAsync>[0]['data']) =>
      mutation.mutateAsync({ data: payload }),
  };
}

/** Save the per-org override row. On success, invalidate the org +
 * boot queries. `orgId` is captured in the wrapper closure — the
 * caller passes only the body. */
export function useUpdateOrgBranding(orgId: string) {
  const queryClient = useQueryClient();
  const mutation = useUpdateBrandingOrgRaw({
    mutation: {
      onSuccess: async () => {
        await Promise.all([
          queryClient.invalidateQueries({ queryKey: getBrandingOrgQueryKey(orgId) }),
          queryClient.invalidateQueries({ queryKey: getBrandingBootQueryKey() }),
        ]);
      },
    },
  });
  return {
    ...mutation,
    mutate: (payload: Parameters<typeof mutation.mutate>[0]['data']) =>
      mutation.mutate({ orgId, data: payload }),
    mutateAsync: (payload: Parameters<typeof mutation.mutateAsync>[0]['data']) =>
      mutation.mutateAsync({ orgId, data: payload }),
  };
}

/** Reset the per-org override row (delete). `orgId` is captured in
 * the wrapper closure — the caller passes no args. */
export function useDeleteOrgBranding(orgId: string) {
  const queryClient = useQueryClient();
  const mutation = useDeleteBrandingOrgRaw({
    mutation: {
      onSuccess: async () => {
        await Promise.all([
          queryClient.invalidateQueries({ queryKey: getBrandingOrgQueryKey(orgId) }),
          queryClient.invalidateQueries({ queryKey: getBrandingBootQueryKey() }),
        ]);
      },
    },
  });
  return {
    ...mutation,
    mutate: () => mutation.mutate({ orgId }),
    mutateAsync: () => mutation.mutateAsync({ orgId }),
  };
}
