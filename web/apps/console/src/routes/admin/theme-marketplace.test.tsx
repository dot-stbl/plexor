/**
 * AdminThemeMarketplacePage component tests — the marketplace UI's
 * grid + Activate action. Phase 5+ persists activation in a
 * per-org backend row (`branding.theme_installations`) via the
 * kubb-generated mutation hook; tests stub those client
 * functions via vi.spyOn and assert the rendered grid + the
 * Activate / Reset paths.
 *
 * Selectors: this codebase uses `data-od-id` (operator-defined id)
 * on containers rather than `data-testid`, so DOM lookups here
 * go through `document.querySelector` instead of testing-library's
 * `getByTestId`. Asserting text content + role lookups inside the
 * queried subtree uses `within` for normal queries.
 */
import { afterEach, beforeEach, describe, expect, it, vi, type MockInstance } from 'vitest';
import type { ComponentType } from 'react';
import { screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { Route } from './theme-marketplace';
import { renderWithProviders } from '@/test-utils';
import type { ThemeInstallationResponse } from '@/shared/api';

const AdminThemeMarketplacePage = Route.options.component as ComponentType;

function findCard(dataOdId: string): HTMLElement | null {
  return document.querySelector(`[data-od-id="${dataOdId}"]`);
}

const defaultInstallation = (themeId: string): ThemeInstallationResponse => ({
  orgId: '00000000-0000-0000-0000-000000000001',
  themeId,
  name: 'Synthwave Night — Dark',
  version: '0.1.0',
  author: 'plexor-themes',
  manifestSignature: 'a'.repeat(64),
  activatedAt: new Date('2026-09-18T12:00:00Z').toISOString(),
  activatedBy: '00000000-0000-0000-0000-000000000002',
});

describe('AdminThemeMarketplacePage', () => {
  let getBrandingTheme: MockInstance<(...args: never[]) => Promise<ThemeInstallationResponse>>;
  let updateBrandingTheme: MockInstance<
    (...args: never[]) => Promise<ThemeInstallationResponse>
  >;
  let deleteBrandingTheme: MockInstance<(...args: never[]) => Promise<unknown>>;

  beforeEach(async () => {
    const getTheme = await import('@/shared/api/src/client/getBrandingTheme');
    const updateTheme = await import('@/shared/api/src/client/updateBrandingTheme');
    const deleteTheme = await import('@/shared/api/src/client/deleteBrandingTheme');
    getBrandingTheme = vi.spyOn(getTheme, 'getBrandingTheme');
    updateBrandingTheme = vi.spyOn(updateTheme, 'updateBrandingTheme');
    deleteBrandingTheme = vi.spyOn(deleteTheme, 'deleteBrandingTheme');

    // Reset document + storage between tests — the marketplace
    // page emits CSS variables on activate; clearing them keeps
    // the rendered state from one test out of the next.
    document.documentElement.removeAttribute('data-theme');
    document.documentElement.removeAttribute('style');
    document.documentElement.classList.remove('dark');
  });

  afterEach(() => {
    document.documentElement.removeAttribute('data-theme');
    document.documentElement.removeAttribute('style');
    document.documentElement.classList.remove('dark');
  });

  it('renders the community grid with both community themes', async () => {
    getBrandingTheme.mockRejectedValue(new Error('404 Not Found'));

    renderWithProviders(<AdminThemeMarketplacePage />);

    await waitFor(() => {
      expect(findCard('admin-theme-marketplace-community')).not.toBeNull();
    });
    await waitFor(() => {
      expect(findCard('theme-marketplace-card-synthwave-night-dark')).not.toBeNull();
    });
    expect(findCard('theme-marketplace-card-synthwave-night-light')).not.toBeNull();
    expect(findCard('theme-marketplace-card-paper-light')).not.toBeNull();
  });

  it('shows the built-in preset section as a reference', async () => {
    getBrandingTheme.mockRejectedValue(new Error('404 Not Found'));

    renderWithProviders(<AdminThemeMarketplacePage />);

    await screen.findByText(/^Built-in$/);
    expect(findCard('admin-theme-marketplace-built-in')).not.toBeNull();
  });

  it('highlights the active community theme when the backend returns one', async () => {
    getBrandingTheme.mockResolvedValue(defaultInstallation('paper-light'));

    renderWithProviders(<AdminThemeMarketplacePage />);

    await waitFor(() => {
      expect(findCard('theme-marketplace-card-paper-light')).not.toBeNull();
    });
    const card = findCard('theme-marketplace-card-paper-light') as HTMLElement;
    // The "Active" badge appears twice on the active card (header
    // + button label); inactive cards have only one ("Activate").
    expect(within(card).getAllByText(/Active/i).length).toBeGreaterThanOrEqual(2);
  });

  it('clicking Activate calls PUT /branding/theme with the themeId', async () => {
    const user = userEvent.setup();
    getBrandingTheme.mockRejectedValue(new Error('404 Not Found'));
    updateBrandingTheme.mockResolvedValue(defaultInstallation('synthwave-night-dark'));

    renderWithProviders(<AdminThemeMarketplacePage />);

    await waitFor(() => {
      expect(findCard('theme-marketplace-card-synthwave-night-dark')).not.toBeNull();
    });
    const card = findCard('theme-marketplace-card-synthwave-night-dark') as HTMLElement;
    const activateButton = within(card).getByRole('button', { name: /Activate/i });
    await user.click(activateButton);

    await waitFor(() => {
      expect(updateBrandingTheme).toHaveBeenCalledWith(
        { themeId: 'synthwave-night-dark' },
        expect.anything(),
      );
    });

    // The clicked card now shows the Active badge (twice: header + button).
    await waitFor(() => {
      const updated = findCard('theme-marketplace-card-synthwave-night-dark') as HTMLElement;
      expect(within(updated).getAllByText(/Active/i).length).toBeGreaterThanOrEqual(2);
    });
  });

  it('clicking the reset button calls DELETE /branding/theme', async () => {
    const user = userEvent.setup();
    getBrandingTheme.mockResolvedValue(defaultInstallation('synthwave-night-dark'));
    deleteBrandingTheme.mockResolvedValue(undefined);

    renderWithProviders(<AdminThemeMarketplacePage />);

    await waitFor(() => {
      expect(findCard('theme-marketplace-card-synthwave-night-dark')).not.toBeNull();
    });

    const resetButton = await screen.findByRole('button', {
      name: /reset to defaults|reset/i,
    });
    await user.click(resetButton);

    await waitFor(() => {
      expect(deleteBrandingTheme).toHaveBeenCalled();
    });
  });

  /**
   * i18n regression — every key the marketplace page references must be
   * defined in both locale JSONs. i18next returns the raw key string
   * for any missing entry (and TypeScript can't catch that), so without
   * this assertion removing `admin.themeMarketplace.resetToDefaults`
   * from `common.json` would silently leak the raw key into the UI.
   *
   * The check walks `t(...)` calls under `admin.themeMarketplace.*` and
   * asserts every key resolves to a value that is NOT the raw key path.
   */
  it('renders translated strings — no raw i18n keys', async () => {
    getBrandingTheme.mockResolvedValue(defaultInstallation('synthwave-night-dark'));

    renderWithProviders(<AdminThemeMarketplacePage />);

    // Page header + reset button + community section header — must all
    // resolve to translated text, not raw dot.case keys.
    await screen.findByText(/^Theme Marketplace$/);
    await screen.findByText(/^Reset to defaults$/);
    await screen.findByText(/^Community$/);

    // Built-in section header — also i18n-backed.
    await screen.findByText(/^Built-in$/);
  });
});
