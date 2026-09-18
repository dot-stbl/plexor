/**
 * LoginPage component tests — the credentials + SSO entry surface for
 * the Plexor console. The page renders email + password fields + an
 * SSO button, runs client-side validation via Zod, calls the
 * kubb-generated postAuthLogin client, and persists the session triple
 * (accessToken / refreshToken / user) to localStorage before routing
 * to `/`.
 *
 * The postAuthLogin client is stubbed via vi.spyOn (see
 * `nock-auth-api.ts`), same shape as the existing branding / audit
 * mocks. The OIDC SSO button uses `window.location.assign` — the test
 * stubs that side effect directly so the route change doesn't blow
 * up jsdom.
 */
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { fireEvent, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { renderWithProviders } from '@/test-utils/render-with-providers';
import { mockAuthService } from '@/test-utils/nock-auth-api';
import { LoginPage } from './login-page';
import {
  clearSession,
  hasValidSession,
  readSession,
  type StoredSession,
} from './session-storage';
import type { PostAuthLogin200 } from '@/shared/api';

// The LoginPage uses `useNavigate` from @tanstack/react-router. The
// `renderWithProviders` helper already wraps with a router, but for
// these tests we drive navigation via localStorage + window.history
// state — calling `navigate({ to: '/' })` inside the success branch
// just pushes to the test router, and we verify the side effect
// (session persisted + token in localStorage) directly.

function makeLoginResponse(overrides: Partial<PostAuthLogin200> = {}): PostAuthLogin200 {
  return {
    accessToken: 'mock-jwt-token-abcdef',
    refreshToken: 'mock-refresh-token-123456',
    expiresIn: 3600,
    user: {
      id: '00000000-0000-0000-0000-000000000001',
      email: 'demo@plexor.test',
      displayName: 'Demo User',
      roles: ['admin'],
    },
    ...overrides,
  };
}

function renderLoginPage() {
  return renderWithProviders(<LoginPage />);
}

describe('LoginPage', () => {
  beforeEach(() => {
    clearSession();
    // window.location is a read-only object in jsdom — we can't
    // reassign `.assign` directly. Stub the SSO side effect with
    // vi.stubGlobal so each test gets a fresh spy and the assertion
    // below can read the captured URL.
    vi.stubGlobal('location', {
      ...window.location,
      assign: vi.fn(),
      href: window.location.href,
      origin: window.location.origin,
      protocol: window.location.protocol,
      host: window.location.host,
      hostname: window.location.hostname,
      port: window.location.port,
      pathname: window.location.pathname,
      search: window.location.search,
      hash: window.location.hash,
    });
  });

  afterEach(() => {
    vi.unstubAllGlobals();
  });

  it('renders email + password fields + SSO button', () => {
    renderLoginPage();

    expect(screen.getByTestId('login-title').textContent).toBe('Sign in to Plexor');
    expect(screen.getByTestId('login-subtitle').textContent).toBe(
      'Use your work email or single sign-on',
    );
    expect(screen.getByTestId('login-email')).toBeInTheDocument();
    expect(screen.getByTestId('login-password')).toBeInTheDocument();
    expect(screen.getByTestId('login-sso')).toHaveTextContent('Continue with SSO');
    expect(screen.getByTestId('login-submit')).toHaveTextContent('Sign in');
  });

  it('shows validation errors when submitting with empty fields', async () => {
    const user = userEvent.setup();
    renderLoginPage();

    // Submit without touching the inputs — both fields are empty.
    const submit = screen.getByTestId('login-submit');
    await user.click(submit);

    await waitFor(() => {
      expect(screen.getByTestId('login-email-error')).toBeInTheDocument();
      expect(screen.getByTestId('login-password-error')).toBeInTheDocument();
    });
    expect(screen.getByTestId('login-email-error').textContent).toBe('Email is required');
    expect(screen.getByTestId('login-password-error').textContent).toBe('Password is required');
  });

  it('persists the session and lands on / after a successful login', async () => {
    const user = userEvent.setup();
    const mocks = mockAuthService();
    mocks.login.mockResolvedValue(makeLoginResponse());

    renderLoginPage();

    await user.type(screen.getByTestId('login-email'), 'demo@plexor.test');
    await user.type(screen.getByTestId('login-password'), 'mock-password');

    // Spy on history.pushState / replaceState — LoginPage uses
    // router.navigate() which under the test router is a no-op for
    // navigation assertions. We verify the side effect instead.
    await user.click(screen.getByTestId('login-submit'));

    await waitFor(() => {
      const session: StoredSession | null = readSession();
      expect(session).not.toBeNull();
      expect(session?.accessToken).toBe('mock-jwt-token-abcdef');
      expect(session?.refreshToken).toBe('mock-refresh-token-123456');
      expect(session?.user.email).toBe('demo@plexor.test');
    });

    expect(mocks.login).toHaveBeenCalledWith(
      expect.objectContaining({ email: 'demo@plexor.test', password: 'mock-password' }),
    );
    expect(hasValidSession()).toBe(true);
  });

  it('shows the invalid-credentials error on 401', async () => {
    const user = userEvent.setup();
    const mocks = mockAuthService();
    const apiError = new Error('401 Unauthorized') as Error & { status?: number; code?: string };
    apiError.status = 401;
    apiError.code = 'auth.invalid_credentials';
    mocks.login.mockRejectedValue(apiError);

    renderLoginPage();

    await user.type(screen.getByTestId('login-email'), 'demo@plexor.test');
    await user.type(screen.getByTestId('login-password'), 'wrong-password');
    await user.click(screen.getByTestId('login-submit'));

    await waitFor(() => {
      expect(screen.getByTestId('login-error')).toBeInTheDocument();
    });
    expect(screen.getByTestId('login-error').textContent).toBe('Invalid email or password');
    // Session must not be persisted on a failed login.
    expect(readSession()).toBeNull();
  });

  it('redirects via window.location.assign when the SSO button is clicked', async () => {
    const user = userEvent.setup();
    renderLoginPage();

    await user.click(screen.getByTestId('login-sso'));

    const assignMock = window.location.assign as unknown as ReturnType<typeof vi.fn>;
    expect(assignMock).toHaveBeenCalledTimes(1);
    const target = assignMock.mock.calls[0]?.[0] as string;
    expect(target).toContain('/auth/oidc/authorize');
    expect(target).toContain('org=00000000-0000-0000-0000-000000000001');
    expect(target).toContain('redirect=%2F');
  });

  it('shows the loading state while submitting', async () => {
    const user = userEvent.setup();
    const mocks = mockAuthService();
    // Never resolves — keep the mutation pending while we assert.
    mocks.login.mockImplementation(
      () =>
        new Promise<PostAuthLogin200>(() => {
          /* intentionally unresolved */
        }),
    );

    renderLoginPage();

    await user.type(screen.getByTestId('login-email'), 'demo@plexor.test');
    await user.type(screen.getByTestId('login-password'), 'mock-password');

    // Fire the submit and assert loading state appears before the
    // microtask queue drains. fireEvent.click is synchronous enough
    // to capture the loading state immediately after the handler runs.
    fireEvent.click(screen.getByTestId('login-submit'));

    await waitFor(() => {
      const submit = screen.getByTestId('login-submit');
      expect(submit).toBeDisabled();
      expect(submit.getAttribute('aria-busy')).toBe('true');
    });
    expect(screen.getByTestId('login-submit').textContent).toBe('Signing in…');
  });
});
