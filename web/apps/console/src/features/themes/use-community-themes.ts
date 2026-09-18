/**
 * useCommunityThemes — TanStack Query surface for the theme marketplace.
 *
 * In v1 the marketplace is local: the community themes ship inside the
 * bundle (`@/shared/lib/themes/community-themes`), and "Activate" writes
 * the chosen theme id to localStorage. There is no server roundtrip —
 * these hooks exist as the contract the marketplace UI binds to so
 * that the Phase 5+ switch to a kubb-generated client and a real
 * publisher feed is a one-file swap (the rest of the UI imports
 * `useCommunityThemes` + `useActivateTheme` and doesn't change).
 *
 * Local-storage key is hard-coded here in commit 2; commit 3 extracts
 * it into `@/features/themes/theme-activation` so the boot script
 * (`main.tsx`) can read the same key without an import cycle.
 */
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import {
  listCommunityThemes,
  getCommunityTheme,
  type CommunityTheme,
} from '@/shared/lib/themes';

const THEME_ACTIVATION_KEY = 'plexor.theme.activation';

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
 * localStorage under `plexor.theme.activation`; the boot script in
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
      if (typeof window === 'undefined') {
        throw new Error('localStorage unavailable');
      }
      const theme = getCommunityTheme(themeId);
      if (!theme) {
        throw new Error(`Unknown theme id: ${themeId}`);
      }
      try {
        window.localStorage.setItem(THEME_ACTIVATION_KEY, theme.id);
      } catch {
        // localStorage may be unavailable (private mode, quota). The
        // marketplace still completes — the next reload just won't
        // auto-apply — but the in-page mutation should not throw.
      }
      return theme.id;
    },
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: themeMarketplaceQueryKeys.active() });
    },
  });
}
