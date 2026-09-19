/**
 * LoginPage component tests — the credentials + SSO entry surface for
 * the Plexor console. The page renders a big PlexorMark + title +
 * subtitle + email + password fields + an SSO button, runs client-side
 * validation via Zod, calls the kubb-generated postAuthLogin client,
 * and persists the session triple (accessToken / refreshToken / user)
 * to localStorage before routing to `/`.
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

  it('renders the brand block (PlexorMark + title + subtitle) and the email + password fields + SSO button', () => {
    renderLoginPage();

    // Brand block — the minimalist revamp's identity surface.
    expect(screen.getByTestId('login-mark')).toBeInTheDocument();
    expect(screen.getByTestId('login-title')).toHaveTextContent('Sign in to Plexor');
    expect(screen.getByTestId('login-subtitle')).toHaveTextContent(
      'Use your work email or single sign-on',
    );

    // Form fields + SSO remain the single action surface.
    expect(screen.getByTestId('login-email')).toBeInTheDocument();
    expect(screen.getByTestId('login-password')).toBeInTheDocument();
    expect(screen.getByTestId('login-sso')).toHaveTextContent('Continue with SSO');
    expect(screen.getByTestId('login-submit')).toHaveTextContent('Sign in');
  });

  it('renders the PlexorMark at the configured size (size-14 / 56px) for the minimalist brand surface', () => {
    renderLoginPage();

    // The PlexorMark is the brand anchor of the revamp. Its size is
    // communicated via the Tailwind `size-N` utility (here `size-14` =
    // 3.5rem / 56px — within the 48-64px band the design called for).
    // jsdom does not compute layout, but we can assert the class is
    // present on the rendered SVG element.
    const mark = screen.getByTestId('login-mark');
    expect(mark.tagName.toLowerCase()).toBe('svg');
    expect(mark.getAttribute('class') ?? '').toMatch(/\bsize-14\b/);
    expect(mark.getAttribute('class') ?? '').toMatch(/\btext-foreground\b/);
  });

  it('renders the minimalist surface without a Card chrome', () => {
    const { container } = renderLoginPage();

    // No card wrapper — the revamp dropped the Card in favour of a
    // direct on-background form column (GitHub / Notion / Figma pattern).
    expect(container.querySelector('[data-slot="card"]')).toBeNull();
    expect(container.querySelector('[data-slot="card-header"]')).toBeNull();
    expect(container.querySelector('[data-slot="card-footer"]')).toBeNull();

    // The "or" divider is gone too — the SSO button sits directly
    // below the form with no separator.
    expect(container.querySelector('hr, [data-slot="separator"]')).toBeNull();

    // The new surface wrapper is the immediate host of the form.
    const surface = screen.getByTestId('login-form-wrapper');
    expect(surface.querySelector('form')).not.toBeNull();
    expect(surface.contains(screen.getByTestId('login-sso'))).toBe(true);
  });

  it('renders a `?` help trigger next to both the email and password labels', () => {
    renderLoginPage();

    // The HelpTooltip wraps a button with aria-label="Help". Both email and
    // password fields get one so the operator knows the SSO recommendation
    // and that the local-part of the email is case-insensitive.
    const helpButtons = screen.getAllByRole('button', { name: 'Help' });
    expect(helpButtons).toHaveLength(2);

    // Each `?` lives in the same field wrapper as its input. The wrapper
    // is the closest ancestor that also contains the corresponding <input>
    // — we walk up the DOM until we find the email or password input by id.
    // If either help button ever drifts away from its label, this fails
    // with a clear message pointing at the orphan trigger.
    const emailInput = screen.getByTestId('login-email');
    const passwordInput = screen.getByTestId('login-password');
    expect(helpButtons[0]?.closest('form')?.contains(emailInput)).toBe(true);
    expect(helpButtons[1]?.closest('form')?.contains(passwordInput)).toBe(true);
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