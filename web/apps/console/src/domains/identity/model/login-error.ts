/**
 * Maps a failed postAuthLogin() error to an i18n key. The backend's
 * 401 + problem+json body carries the reason via `code`:
 *
 *   auth.invalid_credentials → bad email or bad password (default for
 *                              any 401 without a more specific code)
 *   auth.locked              → account locked after N failed attempts
 *
 * The status code alone can't disambiguate: both bad credentials and
 * a locked account map to 401 (the backend never reveals which users
 * exist, so even a valid email on a locked account returns 401).
 *
 * Anything non-401 (network drop, 5xx) collapses to the generic key —
 * the surface copy doesn't promise a specific cause for upstream
 * failures.
 */
export function loginErrorKey(error: Error | null): string | null {
  if (error === null) {
    return null;
  }

  const status = readStatus(error);
  const code = readCode(error);

  if (status === 401) {
    if (code === 'auth.locked') {
      return 'auth.login.errors.config';
    }
    return 'auth.login.errors.invalid';
  }
  if (status !== null && (status === 0 || status >= 500)) {
    return 'auth.login.errors.network';
  }
  return 'auth.login.errors.generic';
}

function readStatus(error: Error): number | null {
  const candidate = error as Error & { status?: unknown; response?: { status?: unknown } };
  if (typeof candidate.status === 'number') {
    return candidate.status;
  }
  if (
    candidate.response !== undefined &&
    candidate.response !== null &&
    typeof candidate.response.status === 'number'
  ) {
    return candidate.response.status;
  }
  return null;
}

function readCode(error: Error): string | null {
  const candidate = error as Error & {
    code?: unknown;
    body?: { code?: unknown };
    data?: { code?: unknown };
  };
  if (typeof candidate.code === 'string') {
    return candidate.code;
  }
  if (
    candidate.body !== undefined &&
    candidate.body !== null &&
    typeof candidate.body.code === 'string'
  ) {
    return candidate.body.code;
  }
  if (
    candidate.data !== undefined &&
    candidate.data !== null &&
    typeof candidate.data.code === 'string'
  ) {
    return candidate.data.code;
  }
  return null;
}
