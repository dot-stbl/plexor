/**
 * Auth fixtures — shared by MSW handlers (`shared/api/mocks/handlers.ts`)
 * and any component test that needs a seeded session (e.g. the home
 * page beforeLoad, the settings page, etc.).
 *
 * The single-user dev session lives here so `writeSession()` in tests
 * reads from one source of truth. Real multi-user fixtures land when
 * per-org + per-team identity land (Phase 2+).
 */

import type { StoredSession } from '@/shared/lib/session';

/**
 * A stable dev session — used by `routes/index.test.tsx`'s `beforeEach`
 * and any test that needs an authenticated route. The `id` is the
 * canonical per-user storage key (`plexor-preferences:${user.id}`),
 * so changing the value here changes every test that depends on the
 * per-user theme storage.
 */
export const DEV_SESSION: StoredSession = {
  accessToken: 'jwt.dev.access',
  refreshToken: 'jwt.dev.refresh',
  expiresAt: Date.now() + 60 * 60 * 1000,
  user: {
    id: 'user-dev-1',
    email: 'a.sergeev@plexor.local',
    displayName: 'Alexey Sergeev',
    roles: ['viewer', 'operator'],
  },
};
