/**
 * useCommunityThemes — TanStack Query surface for the theme marketplace.
 *
 * In v1 the marketplace is local: the community themes ship inside the
 * bundle (`@/shared/lib/themes/community-themes`), and "Activate"
 * writes the chosen theme id to localStorage via
 * `./theme-activation`. There is no server roundtrip — these hooks
 * exist as the contract the marketplace UI binds to so that the
 * Phase 5+ switch to a kubb-generated client and a real publisher
 * feed is a one-file swap (the rest of the UI imports
 * `useCommunityThemes` + `useActivateTheme` and doesn't change).
 */
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import {
  listCommunityThemes,
  getCommunityTheme,
  type CommunityTheme,
} from '@/shared/lib/themes';
import {
  getActiveThemeId,
  setActiveThemeId,
} from './theme-activation';

/** Query key factory — keeps cache invalidation paths honest. */
export const themeMarketplaceQueryKeys = {
  all: () => ['theme-activation'] as const,
  community: () => ['theme-activation', 'community'] as const,
  active: () => ['theme-activation', 'active'] as const,
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
 * Activate a community theme. The mutation writes the theme id to
 * localStorage (via `./theme-activation`); the boot script in
 * `main.tsx` reads the same key on the next reload and applies the
 * preset before first paint.
 *
 * The mutationFn validates the id against the registry — a stale id
 * (e.g. a community theme that was removed in a later bundle) throws
 * instead of writing a phantom value.
 */
export function useActivateTheme() {
  const queryClient = useQueryClient();
  return useMutation<string, Error, string>({
    mutationFn: async (themeId: string) => {
      const theme = getCommunityTheme(themeId);
      if (!theme) {
        throw new Error(`Unknown theme id: ${themeId}`);
      }
      setActiveThemeId(theme.id);
      return theme.id;
    },
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: themeMarketplaceQueryKeys.active() });
    },
  });
}

/**
 * Hook that reads the active theme id from localStorage. Returns
 * `null` when no theme has been activated yet; the boot script in
 * `main.tsx` writes the value before React mounts, so on first render
 * the lookup is already populated.
 */
export function useActiveThemeId(): string | null {
  return getActiveThemeId();
}
