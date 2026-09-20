// Handmade mock — MSW handler + faker fixture for POST /auth/login, an
// endpoint NOT yet in contracts/plexor.openapi.yaml (Phase 4.6 sigil spec).
// The client half lives in shared/api/auth.ts.
//
// Moved out of the kubb clean-wiped dir (src/shared/api/src) — the previous
// hand-mirrored copies were deleted by every `bun run generate`, silently
// breaking the dev:mock worker until someone restored them.
//
// TODO(contract): add /auth/login to the contract, regen, wire the kubb
// handler in handlers.ts, and delete this module + shared/api/auth.ts.
import { faker } from '@faker-js/faker';
import { http } from 'msw';
import type { AuthLoginResponse } from '../../auth';

export function createPostAuthLogin200(data?: Partial<AuthLoginResponse>): AuthLoginResponse {
  return {
    accessToken: `mock-jwt.${faker.string.alphanumeric(32)}.${faker.string.alphanumeric(8)}`,
    refreshToken: `mock-refresh.${faker.string.alphanumeric(40)}`,
    expiresIn: 3600,
    user: {
      id: faker.string.uuid(),
      email: faker.internet.email().toLowerCase(),
      displayName: faker.person.fullName(),
      roles: faker.helpers.arrayElements(['admin', 'editor', 'viewer'], { min: 1, max: 2 }),
    },
    ...(data ?? {}),
  };
}

export function postAuthLoginHandler(
  data?:
    | AuthLoginResponse
    | ((info: Parameters<Parameters<typeof http.post>[1]>[0]) => Response | Promise<Response>),
) {
  return http.post('/auth/login', function handler(info) {
    if (typeof data === 'function') {
      return data(info);
    }
    // Default to a faker fixture when no data is supplied — the original
    // kubb stub JSON.stringified `undefined`, leaving an empty body the
    // FE's postAuthLogin() couldn't parse (see fix/fe/mocks).
    return new Response(JSON.stringify(data ?? createPostAuthLogin200()), {
      status: 200,
      headers: { 'Content-Type': 'application/json' },
    });
  });
}
