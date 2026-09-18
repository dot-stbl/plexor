/**
 * mockAuthService — wraps the kubb-generated auth client functions in
 * vi.spyOn handles so component tests can stub postAuthLogin return
 * values without touching the real fetch path.
 *
 * The login page consumes postAuthLogin via the kubb-generated TanStack
 * Query hook (`usePostAuthLogin`), which calls the kubb-generated
 * client. Spying on the client is the same pattern used for
 * `mockBrandingService` / `mockAuditService`.
 *
 * Usage:
 *   const mocks = mockAuthService();
 *   mocks.login.mockResolvedValue({ accessToken: '...', refreshToken: '...', expiresIn: 3600, user: { ... } });
 *   await user.click(submitButton);
 *   expect(mocks.login).toHaveBeenCalledWith(
 *     { email: '...', password: '...' },
 *     expect.anything(),  // kubb config (signal, etc.)
 *   );
 */
import { vi, type MockInstance } from 'vitest';
import * as postAuthLoginModule from '@/shared/api/src/client/postAuthLogin';

export interface AuthServiceMocks {
  login: MockInstance<(typeof postAuthLoginModule)['postAuthLogin']>;
}

export function mockAuthService(): AuthServiceMocks {
  return {
    login: vi.spyOn(postAuthLoginModule, 'postAuthLogin'),
  };
}
