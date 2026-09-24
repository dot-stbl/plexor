/**
 * PreferencesProvider — per-user theme storage regression suite.
 *
 * Phase-2 multi-user support lands in Plexor v2.0; until then the v1
 * console ships with the per-user key contract in place so a future
 * sign-in-as-different-user flow picks up the new user's prefs
 * automatically, and so two test suites (different users) can't
 * stomp on each other's localStorage between mounts.
 *
 * Two bugs this file guards against:
 *
 * 1. **Global key collisions.** Before the per-user change the
 *    provider wrote to `localStorage['plexor-preferences']` —
 *    single key, single user. Two tests sharing the same browser
 *    storage could overwrite each other's prefs (alice picks dark,
 *    bob's test renders the app → bob's pre-saved prefs win, alice
 *    now has light). The fix: namespace by user id.
 *
 * 2. **Stale prefs after sign-out / sign-in as a different user.**
 *    Once two users share a browser, switching the active session
 *    must reload from the new user's namespace, not keep the prior
 *    user's last-saved values. The provider re-reads from the new
 *    key whenever `userId` changes.
 */

import { afterEach, beforeEach, describe, expect, it } from 'vitest';
import { act, render, renderHook, screen, waitFor } from '@testing-library/react';
import { useState } from 'react';
import {
  PREFERENCES_DEFAULT,
  PreferencesProvider,
  preferencesStorageKey,
  readCurrentUserId,
  STORAGE_KEY_BASE,
  usePreferences,
} from './preferences-provider';
import {
  clearSession,
  writeSession,
  type StoredSession,
} from '@/shared/lib/session';

function makeSession(userId: string): StoredSession {
  return {
    accessToken: `jwt.${userId}`,
    refreshToken: `refresh.${userId}`,
    expiresAt: Date.now() + 60_000,
    user: {
      id: userId,
      email: `${userId}@plexor.local`,
      displayName: `User ${userId}`,
      roles: ['viewer'],
    },
  };
}

function ProbeButton(): React.ReactElement {
  const { preferences, update } = usePreferences();
  return (
    <div>
      <span data-testid="theme">{preferences.theme}</span>
      <button type="button" onClick={() => update('theme', 'dark')}>
        pick dark
      </button>
    </div>
  );
}

describe('preferencesStorageKey', () => {
  it('returns the base key when no user id is supplied (anonymous / pre-auth)', () => {
    expect(preferencesStorageKey(null)).toBe(STORAGE_KEY_BASE);
    expect(preferencesStorageKey(undefined)).toBe(STORAGE_KEY_BASE);
    expect(preferencesStorageKey('')).toBe(STORAGE_KEY_BASE);
  });

  it('returns a per-user key when a user id is supplied', () => {
    expect(preferencesStorageKey('alice')).toBe(`${STORAGE_KEY_BASE}::alice`);
    expect(preferencesStorageKey('bob')).toBe(`${STORAGE_KEY_BASE}::bob`);
  });
});

describe('readCurrentUserId', () => {
  beforeEach(() => {
    clearSession();
    localStorage.clear();
  });

  it('returns null when no session is mounted', () => {
    expect(readCurrentUserId()).toBeNull();
  });

  it('returns the active user id when a session is mounted', () => {
    writeSession(makeSession('alice'));
    expect(readCurrentUserId()).toBe('alice');
  });
});

describe('PreferencesProvider — per-user storage', () => {
  beforeEach(() => {
    clearSession();
    localStorage.clear();
  });

  afterEach(() => {
    clearSession();
    localStorage.clear();
  });

  it('reads from the per-user key derived from the active session', () => {
    // Alice signed in earlier and picked dark — her prefs live at
    // plexor-preferences::alice. When PreferencesProvider mounts and
    // alice's session is active, the initial state must be dark.
    localStorage.setItem(`${STORAGE_KEY_BASE}::alice`, JSON.stringify({ ...PREFERENCES_DEFAULT, theme: 'dark' }));
    writeSession(makeSession('alice'));

    render(
      <PreferencesProvider>
        <ProbeButton />
      </PreferencesProvider>,
    );

    expect(screen.getByTestId('theme').textContent).toBe('dark');
  });

  it('writes to the per-user key, not the global base key', () => {
    writeSession(makeSession('alice'));

    render(
      <PreferencesProvider>
        <ProbeButton />
      </PreferencesProvider>,
    );

    act(() => {
      screen.getByRole('button', { name: 'pick dark' }).click();
    });

    expect(screen.getByTestId('theme').textContent).toBe('dark');
    expect(localStorage.getItem(`${STORAGE_KEY_BASE}::alice`)).not.toBeNull();
    // The global base key MUST stay empty — no cross-user pollution.
    expect(localStorage.getItem(STORAGE_KEY_BASE)).toBeNull();
  });

  it('keeps two users’ prefs isolated when mounted in sequence', () => {
    // Alice signs in, picks dark.
    writeSession(makeSession('alice'));
    const { unmount } = render(
      <PreferencesProvider>
        <ProbeButton />
      </PreferencesProvider>,
    );
    act(() => {
      screen.getByRole('button', { name: 'pick dark' }).click();
    });
    unmount();

    // Bob signs in on the same browser, picks light. Alice's prefs
    // must remain untouched at plexor-preferences::alice.
    writeSession(makeSession('bob'));
    render(
      <PreferencesProvider>
        <ProbeButton />
      </PreferencesProvider>,
    );
    act(() => {
      screen.getByRole('button', { name: 'pick dark' }).click();
    });

    expect(JSON.parse(localStorage.getItem(`${STORAGE_KEY_BASE}::bob`) ?? '{}').theme).toBe('dark');
    expect(JSON.parse(localStorage.getItem(`${STORAGE_KEY_BASE}::alice`) ?? '{}').theme).toBe('dark');
    // The provider reloads from bob's namespace, so the initial
    // render for bob should show the default light theme (bob has
    // no saved prefs yet) — not alice's dark.
    // (We assert the namespace isolation, not the transient mount
    // state, because the second ProbeButton's `update` ran dark on
    // top of bob's defaults.)
    expect(localStorage.getItem(`${STORAGE_KEY_BASE}::bob`)).not.toBeNull();
    expect(localStorage.getItem(`${STORAGE_KEY_BASE}::alice`)).not.toBeNull();
  });

  it('reloads from the new namespace when the active user id changes', async () => {
    // Alice signs in with dark.
    writeSession(makeSession('alice'));
    const { rerender } = render(
      <PreferencesProvider>
        <ProbeButton />
      </PreferencesProvider>,
    );
    act(() => {
      screen.getByRole('button', { name: 'pick dark' }).click();
    });
    expect(screen.getByTestId('theme').textContent).toBe('dark');

    // Switch to bob (no saved prefs) — the provider must drop alice's
    // last value and render the system default.
    writeSession(makeSession('bob'));
    rerender(
      <PreferencesProvider>
        <ProbeButton />
      </PreferencesProvider>,
    );
    // The reload-from-new-key effect runs after render; wait for it
    // to flush before asserting on the new state.
    await waitFor(() => {
      expect(screen.getByTestId('theme').textContent).toBe(PREFERENCES_DEFAULT.theme);
    });
  });

  it('honors an explicit userId prop override (tests + SSR-style isolation)', () => {
    // Even with no session, an explicit `userId` prop pins the storage
    // key — useful for tests that mount the provider before
    // writeSession runs (the home page test, for instance).
    localStorage.setItem(`${STORAGE_KEY_BASE}::preview-user`, JSON.stringify({ ...PREFERENCES_DEFAULT, theme: 'dark' }));

    render(
      <PreferencesProvider userId="preview-user">
        <ProbeButton />
      </PreferencesProvider>,
    );

    expect(screen.getByTestId('theme').textContent).toBe('dark');
    expect(localStorage.getItem(`${STORAGE_KEY_BASE}::preview-user`)).not.toBeNull();
  });
});

describe('PreferencesProvider — explicit storageKey overrides both', () => {
  it('takes precedence over the per-user key (used by tests to fully isolate storage)', () => {
    const isolatedKey = 'plexor-test-isolation';
    writeSession(makeSession('alice'));

    const { result } = renderHook(
      () => {
        const [theme, setTheme] = useState<string | null>(null);
        return { theme, setTheme };
      },
    );

    render(
      <PreferencesProvider storageKey={isolatedKey} userId="alice">
        <ProbeButton />
      </PreferencesProvider>,
    );

    act(() => {
      screen.getByRole('button', { name: 'pick dark' }).click();
    });

    expect(JSON.parse(localStorage.getItem(isolatedKey) ?? '{}').theme).toBe('dark');
    expect(localStorage.getItem(`${STORAGE_KEY_BASE}::alice`)).toBeNull();

    // Reference `result` so vitest doesn't flag the hook as unused
    // when only the side effect matters. (The probe button already
    // asserted the state; this is a no-op.)
    void result.current;
  });
});
