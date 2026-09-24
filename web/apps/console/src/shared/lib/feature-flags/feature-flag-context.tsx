import { createContext, useCallback, useContext, useState, type ReactNode } from 'react';

/**
 * Feature flag keys — the union of every toggleable capability in the
 * console. New flags add a new literal here so the type system flags
 * every consumer that needs updating; renaming a flag is a global
 * find/replace that's caught by `rg 'oldFlagName' src/`.
 *
 * Convention:
 * - `auth.*` — login / SSO / identity provider visibility
 * - `sidebar.*` — sections of the contextual sidebar (visible to nav users)
 *
 * Flags live in localStorage under `plexor.feature-flags` as a JSON
 * blob merged over `DEFAULT_FLAGS`. New keys default to `false` so a
 * flag that's been added but not yet defaulted can't accidentally
 * light up — flip `DEFAULT_FLAGS` in the same commit that adds the
 * key.
 */
export type FeatureFlag =
  | 'auth.showGoogle'
  | 'auth.showGitHub'
  | 'auth.showOidc'
  | 'auth.showLdap'
  | 'sidebar.showAdminSection'
  | 'sidebar.showObservability'
  | 'sidebar.showBillingSection'
  | 'sidebar.showStorageSection'
  | 'sidebar.showNetworkSection';

export type FeatureFlags = Record<FeatureFlag, boolean>;

export const DEFAULT_FLAGS: FeatureFlags = {
  'auth.showGoogle': true,
  'auth.showGitHub': true,
  'auth.showOidc': true,
  'auth.showLdap': true,
  'sidebar.showAdminSection': true,
  'sidebar.showObservability': true,
  'sidebar.showBillingSection': false,
  'sidebar.showStorageSection': true,
  'sidebar.showNetworkSection': true,
};

const STORAGE_KEY = 'plexor.feature-flags';

interface FeatureFlagContextValue {
  flags: FeatureFlags;
  setFlag: (key: FeatureFlag, value: boolean) => void;
  isEnabled: (key: FeatureFlag) => boolean;
}

const Ctx = createContext<FeatureFlagContextValue | null>(null);

function loadInitialFlags(): FeatureFlags {
  if (typeof window === 'undefined') {
    return DEFAULT_FLAGS;
  }
  try {
    const raw = window.localStorage.getItem(STORAGE_KEY);
    if (!raw) return DEFAULT_FLAGS;
    const parsed = JSON.parse(raw) as Partial<FeatureFlags>;
    return { ...DEFAULT_FLAGS, ...parsed };
  } catch {
    return DEFAULT_FLAGS;
  }
}

export function FeatureFlagProvider({ children }: { children: ReactNode }) {
  const [flags, setFlags] = useState<FeatureFlags>(loadInitialFlags);

  const setFlag = useCallback((key: FeatureFlag, value: boolean) => {
    setFlags((prev) => {
      const next = { ...prev, [key]: value };
      if (typeof window !== 'undefined') {
        try {
          window.localStorage.setItem(STORAGE_KEY, JSON.stringify(next));
        } catch {
          // ignore (private mode, quota exceeded)
        }
      }
      return next;
    });
  }, []);

  const isEnabled = useCallback(
    (key: FeatureFlag) => Boolean(flags[key]),
    [flags],
  );

  return <Ctx.Provider value={{ flags, setFlag, isEnabled }}>{children}</Ctx.Provider>;
}

/**
 * Read a single flag. Returns `false` when used outside the provider —
 * the safe default for "feature off" rather than the crash-on-mistype
 * behavior of `useFeatureFlags`.
 */
export function useFeatureFlag(key: FeatureFlag): boolean {
  const ctx = useContext(Ctx);
  return ctx?.isEnabled(key) ?? false;
}

/**
 * Read the full flag set + mutator. Throws when used outside the
 * provider — the mutator needs a guarantee the storage key is wired,
 * and "no-op silently" would be a worse surprise than a hard fail.
 */
export function useFeatureFlags(): FeatureFlagContextValue {
  const ctx = useContext(Ctx);
  if (!ctx) {
    throw new Error('useFeatureFlags must be used inside FeatureFlagProvider');
  }
  return ctx;
}