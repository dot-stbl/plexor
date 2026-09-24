/**
 * mockAuthService — wraps the auth client function in a vi.spyOn handle so
 * component tests can stub postAuthLogin return values without touching
 * the real fetch path.
 *
 * The login page calls postAuthLogin (kubb-generated client re-exported
 * via shared/api/index.ts → shared/api/src/client/postAuthLogin.ts —
 * POST /auth/login is generated from contracts/plexor.openapi.yaml).
 * Spying on the barrel is the same pattern used for
 * `mockBrandingService` / `mockAuditService`.
 *
 * Usage:
 *   const mocks = mockAuthService();
 *   mocks.login.mockResolvedValue({ accessToken: '...', refreshToken: '...', expiresIn: 3600, user: { ... } });
 *   await user.click(submitButton);
 *   expect(mocks.login).toHaveBeenCalledWith(
 *     { email: '...', password: '...' },
 *     expect.anything(),  // client config (signal, etc.)
 *   );
 */
import { vi, type MockInstance } from 'vitest';
import * as postAuthLoginModule from '@/shared/api';

export interface AuthServiceMocks {
  login: MockInstance<typeof postAuthLoginModule.postAuthLogin>;
}

export function mockAuthService(): AuthServiceMocks {
  return {
    login: vi.spyOn(postAuthLoginModule, 'postAuthLogin'),
  };
}
