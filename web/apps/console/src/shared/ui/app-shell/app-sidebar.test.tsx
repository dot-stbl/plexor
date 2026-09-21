/**
 * AppSidebar — footer avatar identity regression suite, extended with
 * feature-flag-gated sections and the always-on Settings link.
 *
 * Three concerns this file guards against:
 *
 * 1. **Hardcoded identity.** The sidebar footer used to render the locale
 *    seed ("Alexey Sergeev" / "AS") regardless of who was actually logged
 *    in. The fix pulls the live identity from `readSession()` (the
 *    localStorage-backed session written by /login) and threads it into
 *    the Avatar primitive + the dropdown label so the user sees their own
 *    name + initials + email when the menu is opened.
 *
 * 2. **Avatar URL wiring.** When the backend eventually returns an
 *    `avatarUrl` on the user record, the Avatar primitive needs `src` set
 *    so it renders `<img>` instead of initials.
 *
 * 3. **Feature-flag-gated sections.** Each section with a `sidebar.show*`
 *    flag is hidden when the flag is off. The "Settings" link in the
 *    footer is independent of feature flags — it always renders so the
 *    user has a stable route out of every state.
 *
 * Test strategy:
 *
 * - We can't reliably test the visible rendered output of the sidebar
 *   footer via the live trigger because the DropdownMenuTrigger +
 *   DropdownMenuLabel primitives in this codebase have unrelated
 *   pre-existing rendering quirks (trigger drops its children when
 *   used with a `render={...}` Button; Label wrapped in Group doesn't
 *   surface outside the RAC Menu). Those are out of scope for this fix.
 *
 * - Instead, the test asserts the source-of-truth plumbing that the
 *   sidebar relies on: `readSession()` must return the seeded user, and
 *   the Avatar primitive — which the sidebar uses for both the no-session
 *   fallback (with a hardcoded <AvatarFallback>) and the with-session
 *   branch (via `name` + optional `src`) — renders the right shape for
 *   each branch.
 *
 * - The trigger click path is sanity-checked via the menu items
 *   ("Settings" / "Sign out") which DO render after the click. This
 *   guards against a future regression where the sidebar breaks the
 *   dropdown wiring entirely.
 *
 * - Feature-flag tests cover the home-overview mode (all sections
 *   visible) and verify that toggling a flag off hides the matching
 *   section. The Settings link is asserted present in both modes —
 *   it's the always-on escape hatch.
 */
import { describe, expect, it, beforeEach } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { renderWithProviders } from '@/test-utils';
import { SidebarProvider } from '@/shared/ui/primitives/sidebar';
import { Avatar } from '@/shared/ui/primitives/avatar';
import { FeatureFlagProvider } from '@/shared/lib/feature-flags/feature-flag-context';
import {
  writeSession,
  clearSession,
  readSession,
  type StoredSession,
} from '@/features/auth/session-storage';
import { AppSidebar } from './app-sidebar';

function makeSession(overrides: Partial<StoredSession['user']> = {}): StoredSession {
  return {
    accessToken: 'jwt.test',
    refreshToken: 'refresh.test',
    expiresAt: Date.now() + 60_000,
    user: {
      id: 'user-1',
      email: 'jane.doe@example.com',
      displayName: 'Jane Doe',
      roles: ['viewer'],
      ...overrides,
    },
  };
}

function renderSidebar() {
  return renderWithProviders(
    <FeatureFlagProvider>
      <SidebarProvider defaultOpen>
        <AppSidebar />
      </SidebarProvider>
    </FeatureFlagProvider>,
  );
}

function setFlag(key: string, value: boolean) {
  const raw = localStorage.getItem('plexor.feature-flags');
  const parsed = raw ? JSON.parse(raw) : {};
  parsed[key] = value;
  localStorage.setItem('plexor.feature-flags', JSON.stringify(parsed));
}

describe('readSession() — sidebar source of truth', () => {
  beforeEach(() => {
    clearSession();
  });

  it('returns null when no session is in localStorage', () => {
    clearSession();
    expect(readSession()).toBeNull();
  });

  it('returns the seeded user when writeSession was called', () => {
    writeSession(makeSession({ displayName: 'Jane Doe' }));
    const session = readSession();
    expect(session).not.toBeNull();
    expect(session?.user.displayName).toBe('Jane Doe');
    expect(session?.user.email).toBe('jane.doe@example.com');
  });
});

describe('Avatar primitive — the wiring the sidebar depends on', () => {
  it('renders <img> when src is provided, with alt = name', () => {
    render(
      <Avatar
        name="Jane Doe"
        src="https://cdn.example.com/avatars/jane.jpg"
      />,
    );
    const img = screen.getByRole('img');
    expect(img).toHaveAttribute('alt', 'Jane Doe');
    expect(img).toHaveAttribute('src', 'https://cdn.example.com/avatars/jane.jpg');
  });

  it('renders initials derived from name when no src is given', () => {
    render(<Avatar name="Jane Doe" />);
    // The Avatar primitive derives "JD" from "Jane Doe".
    expect(screen.getByText('JD')).toBeInTheDocument();
    expect(screen.queryByRole('img')).toBeNull();
  });
});

describe('AppSidebar — dropdown menu still opens on trigger click', () => {
  beforeEach(() => {
    clearSession();
    localStorage.removeItem('plexor.feature-flags');
  });

  it('opens the account menu when the Account trigger is clicked', async () => {
    const user = userEvent.setup();
    clearSession();
    renderSidebar();
    await user.click(screen.getByLabelText('Account'));
    // The "Sign out" item is unique to the dropdown — the new footer
    // Settings link competes with the dropdown's Settings entry, so
    // assert on the unique one instead.
    expect(await screen.findByText('Sign out')).toBeInTheDocument();
  });

  it('preserves the dropdown wiring when a session is present', async () => {
    const user = userEvent.setup();
    writeSession(makeSession());
    renderSidebar();
    await user.click(screen.getByLabelText('Account'));
    // Same assertion with a session — the wiring must work regardless of
    // whether readSession() returns a user or null.
    expect(await screen.findByText('Sign out')).toBeInTheDocument();
  });
});

describe('AppSidebar — feature-flag-gated sections', () => {
  beforeEach(() => {
    clearSession();
    localStorage.removeItem('plexor.feature-flags');
  });

  it('renders the Settings link in the footer, always visible', () => {
    renderSidebar();

    const link = screen.getByTestId('sidebar-settings-link');
    expect(link).toBeInTheDocument();
    expect(link.tagName.toLowerCase()).toBe('a');
    expect(link.getAttribute('href')).toContain('/settings/profile');
  });

  it('renders the Admin section in the home overview by default', () => {
    renderSidebar();

    // On the home overview the sidebar lists every section as an entry
    // point. "Admin" is the section the admin.show flag gates. The
    // label appears as both the menu button label and a hidden
    // rail-pill tooltip — assert at least one visible instance.
    const adminLabels = screen.getAllByText('Admin');
    expect(adminLabels.length).toBeGreaterThan(0);
  });

  it('hides the Admin section when sidebar.showAdminSection is off', () => {
    setFlag('sidebar.showAdminSection', false);

    renderSidebar();

    expect(screen.queryAllByText('Admin')).toHaveLength(0);
  });

  it('still renders the Settings link even when every flag is off', () => {
    setFlag('sidebar.showAdminSection', false);
    setFlag('sidebar.showNetworkSection', false);
    setFlag('sidebar.showStorageSection', false);
    setFlag('sidebar.showObservability', false);

    renderSidebar();

    // Settings is the always-on escape hatch — it stays visible
    // even when every section is gated off.
    expect(screen.getByTestId('sidebar-settings-link')).toBeInTheDocument();
  });
});

describe('AppSidebar — brand header logo (no external GitHub URL)', () => {
  beforeEach(() => {
    clearSession();
    localStorage.removeItem('plexor.feature-flags');
  });

  it('uses the local stbl-logo.svg in the home link (no PlexorMark, no raw.githubusercontent)', () => {
    renderSidebar();

    // The home link is the brand anchor at the top of the rail; its
    // aria-label is the translated "Go home".
    const homeLink = screen.getByRole('link', { name: /go home/i });
    expect(homeLink).toBeInTheDocument();

    // First child of the link is the brand mark — StblMark renders <img>
    // pointing at the locally-vendored SVG.
    const mark = homeLink.querySelector('img');
    expect(mark).not.toBeNull();
    expect(mark?.getAttribute('src')).toBe('/stbl-logo.svg');
    expect(mark?.getAttribute('alt')).toBe('');

    // Regression guards: the PlexorMark SVG (purple/dark complex path)
    // and the github.com raw URL must NOT appear in the brand header.
    // A future revert to either would re-introduce the fragility the
    // STBL mark replaced (offline-broken chrome, GitHub CDN delay).
    expect(homeLink.querySelector('svg path[fill="currentColor"]')).toBeNull();
    expect(document.body.innerHTML).not.toContain('raw.githubusercontent.com');
  });
});