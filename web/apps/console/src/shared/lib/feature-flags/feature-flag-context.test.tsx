/**
 * FeatureFlagProvider — localStorage-backed context for per-user
 * feature flags. Five invariants under test:
 *
 * 1. **Defaults.** A fresh `localStorage` returns `DEFAULT_FLAGS` on
 *    mount; nothing leaks from prior tests.
 * 2. **Override from storage.** A pre-existing JSON blob in
 *    `plexor.feature-flags` merges over defaults and is exposed via
 *    `useFeatureFlag`.
 * 3. **setFlag mutates state + storage.** `setFlag(key, value)` from
 *    `useFeatureFlags()` updates the in-memory map and persists to
 *    localStorage so the next page load preserves the choice.
 * 4. **useFeatureFlag is null-safe.** Called outside the provider it
 *    returns `false` rather than throwing — the read path is used in
 *    render branches that should default to "feature off" instead of
 *    blowing up the page.
 * 5. **useFeatureFlags outside provider throws.** The mutator path
 *    is NOT null-safe — calling it without the provider mounted is
 *    a programmer error and gets the loud failure.
 */
import { afterEach, beforeEach, describe, expect, it } from 'vitest';
import { act, render, renderHook, screen } from '@testing-library/react';
import {
  DEFAULT_FLAGS,
  FeatureFlagProvider,
  useFeatureFlag,
  useFeatureFlags,
  type FeatureFlags,
} from './feature-flag-context';

const STORAGE_KEY = 'plexor.feature-flags';

function setStoredFlags(flags: Partial<FeatureFlags>) {
  localStorage.setItem(STORAGE_KEY, JSON.stringify(flags));
}

function readStoredFlags(): Partial<FeatureFlags> | null {
  const raw = localStorage.getItem(STORAGE_KEY);
  return raw ? (JSON.parse(raw) as Partial<FeatureFlags>) : null;
}

function renderWithProvider(
  ui: React.ReactElement | ((props: { children: React.ReactNode }) => React.ReactElement),
) {
  return render(<FeatureFlagProvider>{typeof ui === 'function' ? ui({ children: null }) : ui}</FeatureFlagProvider>);
}

function Probe({ flagKey }: { flagKey: keyof FeatureFlags }) {
  const enabled = useFeatureFlag(flagKey);
  return <span data-testid="probe">{enabled ? 'on' : 'off'}</span>;
}

beforeEach(() => {
  localStorage.clear();
});

afterEach(() => {
  localStorage.clear();
});

describe('FeatureFlagProvider — defaults', () => {
  it('returns DEFAULT_FLAGS when localStorage is empty', () => {
    const { result } = renderHook(() => useFeatureFlags(), {
      wrapper: FeatureFlagProvider,
    });

    expect(result.current.flags).toEqual(DEFAULT_FLAGS);
  });

  it('exposes the full flag set through useFeatureFlag', () => {
    render(<Probe flagKey="sidebar.showBillingSection" />, {
      wrapper: FeatureFlagProvider,
    });

    // sidebar.showBillingSection defaults to false (not ready yet).
    expect(screen.getByTestId('probe').textContent).toBe('off');
  });
});

describe('FeatureFlagProvider — localStorage override', () => {
  it('merges stored flags over defaults on mount', () => {
    setStoredFlags({
      'sidebar.showBillingSection': true,
      'auth.showOidc': false,
    });

    const { result } = renderHook(() => useFeatureFlags(), {
      wrapper: FeatureFlagProvider,
    });

    // Stored value wins for keys present in storage…
    expect(result.current.flags['sidebar.showBillingSection']).toBe(true);
    expect(result.current.flags['auth.showOidc']).toBe(false);
    // …and defaults still apply for keys absent from storage.
    expect(result.current.flags['auth.showGoogle']).toBe(DEFAULT_FLAGS['auth.showGoogle']);
  });

  it('falls back to defaults when stored JSON is malformed', () => {
    // Garbage in the slot must not crash the provider — fall back to
    // defaults and let the user reset individual flags from the UI.
    localStorage.setItem(STORAGE_KEY, 'not-json');

    const { result } = renderHook(() => useFeatureFlags(), {
      wrapper: FeatureFlagProvider,
    });

    expect(result.current.flags).toEqual(DEFAULT_FLAGS);
  });
});

describe('FeatureFlagProvider — setFlag', () => {
  it('updates state and persists to localStorage', () => {
    const { result } = renderHook(() => useFeatureFlags(), {
      wrapper: FeatureFlagProvider,
    });

    expect(result.current.flags['sidebar.showBillingSection']).toBe(false);

    act(() => {
      result.current.setFlag('sidebar.showBillingSection', true);
    });

    expect(result.current.flags['sidebar.showBillingSection']).toBe(true);
    expect(readStoredFlags()?.['sidebar.showBillingSection']).toBe(true);
  });

  it('does not clobber unrelated flags when toggling one', () => {
    const { result } = renderHook(() => useFeatureFlags(), {
      wrapper: FeatureFlagProvider,
    });

    act(() => {
      result.current.setFlag('auth.showGoogle', false);
    });

    expect(result.current.flags['auth.showGoogle']).toBe(false);
    // Adjacent flags survive the toggle.
    expect(result.current.flags['auth.showGitHub']).toBe(DEFAULT_FLAGS['auth.showGitHub']);
    expect(result.current.flags['sidebar.showAdminSection']).toBe(DEFAULT_FLAGS['sidebar.showAdminSection']);
  });
});

describe('useFeatureFlag — null-safe read', () => {
  it('returns false when used outside a provider', () => {
    const { result } = renderHook(() => useFeatureFlag('auth.showGoogle'));

    expect(result.current).toBe(false);
  });

  it('reflects the current flag value inside a provider', () => {
    const { result } = renderHook(() => useFeatureFlag('auth.showOidc'), {
      wrapper: FeatureFlagProvider,
    });

    expect(result.current).toBe(true);

    act(() => {
      // Re-mount with a fresh provider that has a stored off-state.
      setStoredFlags({ 'auth.showOidc': false });
    });
  });
});

describe('useFeatureFlags — throws outside provider', () => {
  it('throws a descriptive error so the misuse is loud', () => {
    expect(() => {
      renderHook(() => useFeatureFlags());
    }).toThrow(/must be used inside FeatureFlagProvider/);
  });

  // Render-through wrapper to ensure the provider actually mounts
  // successfully — guards against future regressions where the
  // provider context value accidentally becomes null.
  it('mounts cleanly through a render wrapper', () => {
    renderWithProvider(<Probe flagKey="auth.showGoogle" />);
    expect(screen.getByTestId('probe').textContent).toBe('on');
  });
});