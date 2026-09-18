/**
 * Theme activation persistence — localStorage-backed read/write of the
 * operator's chosen community theme id. v1 of the marketplace uses
 * local storage as the durable surface; Phase 5+ replaces these two
 * helpers with a kubb-generated client bound to a per-org tenant row
 * (`PUT /api/v1/marketplace/themes/{id}/activate` + `GET …/active`),
 * keeping the call sites intact.
 *
 * The storage key is module-private in spirit — call sites import
 * `getActiveThemeId` / `setActiveThemeId` instead of touching the raw
 * string, so a future rename (or a switch to sessionStorage / IndexedDB)
 * is one file.
 */
const STORAGE_KEY = 'plexor.theme.activation';

/**
 * Read the operator's chosen community theme id from localStorage.
 * Returns `null` when no theme is set, when localStorage is
 * unavailable (private mode / SSR / tests), or when the stored value
 * isn't a string. Callers treat `null` as "use the registry default
 * preset" — never as an error.
 */
export function getActiveThemeId(): string | null {
  if (typeof window === 'undefined') return null;
  try {
    const value = window.localStorage.getItem(STORAGE_KEY);
    return typeof value === 'string' && value.length > 0 ? value : null;
  } catch {
    return null;
  }
}

/**
 * Set the operator's chosen community theme id. Pass `null` to clear
 * (e.g. when the user toggles back to "operator defaults"). localStorage
 * is best-effort — quota / private-mode failures are swallowed so the
 * in-page state change still completes.
 */
export function setActiveThemeId(themeId: string | null): void {
  if (typeof window === 'undefined') return;
  try {
    if (themeId === null) {
      window.localStorage.removeItem(STORAGE_KEY);
    } else {
      window.localStorage.setItem(STORAGE_KEY, themeId);
    }
  } catch {
    // localStorage unavailable — the in-memory query cache still
    // reflects the change for the rest of the session.
  }
}

/** The storage key — exposed for tests and the boot script in main.tsx. */
export const THEME_ACTIVATION_STORAGE_KEY = STORAGE_KEY;
