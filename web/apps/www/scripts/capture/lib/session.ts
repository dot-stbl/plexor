/**
 * Seeds a fake logged-in session + theme/preferences into localStorage
 * BEFORE first paint — same storage contract the console itself uses
 * (`plexor-auth`, `plexor-preferences` + per-user key; see
 * `src/main.tsx`'s `applyThemeEarly()` and
 * `src/shared/lib/preferences-provider.tsx` in web/apps/console, both
 * read-only references — this file does not import from console).
 *
 * Skips the login screen and renders the chosen theme with no
 * flash-of-wrong-theme, exactly like the console's own
 * `scripts/agent/lib/browser-prep.ts` does for `bun run shot`.
 */

import type { Page } from 'playwright';

export type Theme = 'light' | 'dark';

const FAKE_USER_ID = 'capture-admin';

interface FakeSession {
  accessToken: string;
  refreshToken: string;
  expiresAt: number;
  user: { id: string; email: string; displayName: string; roles: string[] };
}

function fakeSession(): FakeSession {
  return {
    accessToken: 'capture-fake-access-token',
    refreshToken: 'capture-fake-refresh-token',
    expiresAt: Date.now() + 1000 * 60 * 60 * 24 * 365,
    user: { id: FAKE_USER_ID, email: 'capture@plexor.local', displayName: 'Plexor Capture', roles: ['admin'] },
  };
}

export async function seedSession(page: Page, theme: Theme): Promise<void> {
  const prefsJson = JSON.stringify({ theme, accent: 'plexor', fontSize: 'medium', language: 'en' });
  await page.addInitScript(
    ({ session, prefsJson: prefs, userId }: { session: FakeSession; prefsJson: string; userId: string }) => {
      try {
        window.localStorage.setItem('plexor-auth', JSON.stringify(session));
        window.localStorage.setItem('plexor-preferences', prefs);
        window.localStorage.setItem(`plexor-preferences::${userId}`, prefs);
      } catch {
        // localStorage unavailable — page falls back to its own default.
      }
    },
    { session: fakeSession(), prefsJson, userId: FAKE_USER_ID },
  );
}
