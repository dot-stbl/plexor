// Handwritten API surface for POST /auth/login.
//
// The endpoint is specified (Phase 4.6 — sigil local-credentials login)
// but contracts/plexor.openapi.yaml does not carry it, so kubb cannot
// generate it. This module lives OUTSIDE the kubb clean-wiped dir (./src)
// on purpose — the previous hand-mirrored copies inside ./src were deleted
// by every `bun run generate` (output.clean: true).
//
// TODO(contract): add /auth/login to contracts/plexor.openapi.yaml, regen
// (web/tooling/codegen), swap the barrel export in ./index.ts to the
// generated one, and delete this module + mocks/handmade/post-auth-login.ts.
import fetch from '@kubb/plugin-client/clients/axios';
import type { Client, RequestConfig, ResponseErrorConfig } from '@kubb/plugin-client/clients/axios';

export interface AuthLoginUser {
  id: string;
  email: string;
  displayName: string;
  roles: string[];
}

export interface AuthLoginRequest {
  email: string;
  password: string;
}

export interface AuthLoginResponse {
  /** JWT (RS256) the console stores and sends as `Authorization: Bearer`. */
  accessToken: string;
  /** Opaque refresh token (rotated on every refresh). */
  refreshToken: string;
  /** Access-token lifetime in seconds. */
  expiresIn: number;
  /** The authenticated user — used to seed the FE user cache. */
  user: AuthLoginUser;
}

/**
 * Sign in with email + password.
 * {@link /auth/login}
 */
export async function postAuthLogin(
  data: AuthLoginRequest,
  config: Partial<RequestConfig> & { client?: Client } = {},
) {
  const { client: request = fetch, ...requestConfig } = config;

  const res = await request<AuthLoginResponse, ResponseErrorConfig<Error>, unknown>({
    method: 'POST',
    url: '/auth/login',
    data,
    ...requestConfig,
  });
  return res.data;
}
