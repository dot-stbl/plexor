/**
 * useCommunityThemes — TanStack Query surface for the theme marketplace.
 *
 * Phase 5+ replaces the v1 localStorage persistence with a per-org
 * backend row (PUT /api/v1/branding/theme). The community theme
 * registry still ships as a bundled module
 * (`@/shared/lib/themes/community-themes`) so the marketplace UI can
 * render the grid; the activation choice now mutates the per-org
 * `theme_installations` row via the kubb-generated mutation hook,
 * and the next page load reads the choice back through
 * `useGetBrandingTheme()`.
 *
 * The kubb-generated client/hooks live at
 * `@/shared/api/src/client/getBrandingTheme`,
 * `updateBrandingTheme`, `deleteBrandingTheme` — generated from the
 * Plexor.Host OpenAPI contract.
 */
import { useQuery, useQueryClient } from '@tanstack/react-query';
import {
  listCommunityThemes,
  type CommunityTheme,
} from '@/shared/lib/themes';
import {
  getBrandingThemeQueryKey,
  useDeleteBrandingTheme,
  useGetBrandingTheme,
  useUpdateBrandingTheme,
} from '@/shared/api';

/** Query key factory — keeps cache invalidation paths honest. */
export const themeMarketplaceQueryKeys = {
  all: () => ['theme-activation'] as const,
  community: () => ['theme-activation', 'community'] as const,
  active: () => getBrandingThemeQueryKey(),
};

/** Read all community themes from the bundled registry. */
export function useCommunityThemes() {
  return useQuery<readonly CommunityTheme[]>({
    queryKey: themeMarketplaceQueryKeys.community(),
    queryFn: () => Promise.resolve(listCommunityThemes()),
    // Bundled, never refetches at runtime.
    staleTime: 60 * 60 * 1000,
    gcTime: Infinity,
  });
}

/**
 * Read the active theme id from the backend. Returns
 * `null` when the kubb query 404s (no theme installed yet);
 * the marketplace UI uses this to highlight the active card.
 */
export function useActiveThemeId(): string | null {
  const { data } = useGetBrandingTheme({
    query: { retry: (_count, error) => !isNotFound(error) },
  });
  return data?.themeId ?? null;
}

/**
 * Activate a community theme. The mutation calls the
 * kubb-generated `useUpdateBrandingTheme` (PUT /api/v1/branding/theme)
 * with just the themeId — the host looks up the canonical
 * manifest in its bundled registry and signs it before
 * persisting. Unknown themeIds throw `UnknownThemeException` on
 * the backend (mapped to 404) and are caught by the UI.
 *
 * Wired through the kubb hook's own `mutation.onSuccess` so the
 * cache invalidation fires on the same mutation instance that
 * flips `isPending` — the marketplace UI's optimistic highlight
 * clears only after the active-theme query has actually
 * invalidated, so the previous "active" highlight doesn't flash
 * off mid-flight. The wrapper unwraps the kubb envelope
 * (`{ data: { themeId } }`) so call sites pass a bare themeId;
 * the kubb response (ThemeInstallationResponse) is forwarded
 * to the per-call `onSuccess` so consumers can extract
 * `response.themeId`.
 */
export function useActivateTheme() {
  const queryClient = useQueryClient();
  const mutation = useUpdateBrandingTheme({
    mutation: {
      onSuccess: () => {
        void queryClient.invalidateQueries({ queryKey: themeMarketplaceQueryKeys.active() });
      },
    },
  });
  return {
    ...mutation,
    mutate: (themeId: string, options?: Parameters<typeof mutation.mutate>[1]) =>
      mutation.mutate({ data: { themeId } }, options),
    mutateAsync: (themeId: string, options?: Parameters<typeof mutation.mutateAsync>[1]) =>
      mutation.mutateAsync({ data: { themeId } }, options),
  };
}

/**
 * Reset to operator defaults by calling the kubb-generated
 * `useDeleteBrandingTheme` (DELETE /api/v1/branding/theme). The
 * next /branding/theme GET returns 404, the boot script falls
 * back to the resolved operator defaults.
 *
 * Same shape as `useActivateTheme` — the kubb hook owns
 * `onSuccess`; the wrapper just unwraps the void-variables
 * envelope so call sites pass no args.
 */
export function useDeactivateTheme() {
  const queryClient = useQueryClient();
  const mutation = useDeleteBrandingTheme({
    mutation: {
      onSuccess: () => {
        void queryClient.invalidateQueries({ queryKey: themeMarketplaceQueryKeys.active() });
      },
    },
  });
  return {
    ...mutation,
    mutate: (options?: Parameters<typeof mutation.mutate>[1]) =>
      mutation.mutate(undefined, options),
    mutateAsync: (options?: Parameters<typeof mutation.mutateAsync>[1]) =>
      mutation.mutateAsync(undefined, options),
  };
}

/**
 * True when the kubb error is the 404 `ThemeInstallation not found`
 * (kubb types the error as `ResponseErrorConfig<GetBrandingTheme404>`);
 * other errors are not 404.
 */
function isNotFound(error: unknown): boolean {
  if (typeof error !== 'object' || error === null) {
    return false;
  }
  const status = (error as { response?: { status?: number } }).response?.status;
  return status === 404;
}
