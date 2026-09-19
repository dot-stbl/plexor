/**
 * Auth session storage — bearer token + refresh token + user identity
 * in localStorage so the page reloads keep the session.
 *
 * Single namespace key (`plexor-auth`) so the trio stays consistent
 * and can be cleared together on logout. Read paths tolerate missing
 * keys (return null) so callers can branch without try/catch.
 *
 * The token format follows the kubb-generated PostAuthLogin200 contract:
 *   accessToken:  JWT (RS256) the console stores as `Authorization: Bearer`
 *   refreshToken: opaque token used to mint a new access token
 *   user:         the authenticated user record (id, email, displayName, roles)
 *
 * Storage is intentionally local-only for the v1 console — the v1.1
 * cookie-jar mode lands in Phase 5 alongside the backend session-cookie
 * contract. Until then the FE owns the lifecycle.
 */

const STORAGE_KEY = 'plexor-auth';

export interface StoredSession {
  readonly accessToken: string;
  readonly refreshToken: string;
  readonly expiresAt: number;
  readonly user: {
    readonly id: string;
    readonly email: string;
    readonly displayName: string;
    readonly roles: ReadonlyArray<string>;
    /** Optional — populated when the backend's user record carries an avatar URL. */
    readonly avatarUrl?: string;
  };
}

function safeStorage(): Storage | null {
  try {
    if (typeof window === 'undefined') return null;
    return window.localStorage;
  } catch {
    return null;
  }
}

export function readSession(): StoredSession | null {
  const storage = safeStorage();
  if (!storage) return null;
  const raw = storage.getItem(STORAGE_KEY);
  if (!raw) return null;
  try {
    const parsed = JSON.parse(raw) as Partial<StoredSession>;
    if (
      typeof parsed.accessToken === 'string' &&
      typeof parsed.refreshToken === 'string' &&
      typeof parsed.expiresAt === 'number' &&
      parsed.user !== undefined &&
      typeof parsed.user.id === 'string'
    ) {
      return parsed as StoredSession;
    }
    return null;
  } catch {
    return null;
  }
}

export function writeSession(session: StoredSession): void {
  const storage = safeStorage();
  if (!storage) return;
  storage.setItem(STORAGE_KEY, JSON.stringify(session));
}

export function clearSession(): void {
  const storage = safeStorage();
  if (!storage) return;
  storage.removeItem(STORAGE_KEY);
}

export function hasValidSession(): boolean {
  const session = readSession();
  if (!session) return false;
  return session.expiresAt > Date.now();
}
