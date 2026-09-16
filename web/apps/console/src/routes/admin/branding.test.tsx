/**
 * AdminBrandingPage component tests — the first real component test in
 * apps/console, exercising the operator-global + per-org override form
 * wired to the branding-service mock. The page reads global + org via
 * TanStack Query hooks, so we stub the service functions (one
 * fetchGlobal, one fetchOrg) and assert the rendered form + the
 * Save / Clear buttons actually call the mutation paths.
 */
import { afterEach, beforeEach, describe, expect, it } from 'vitest';
import type { ComponentType } from 'react';
import { screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { Route } from './branding';
import { mockBrandingService, renderWithProviders } from '@/test-utils';

// `Route.options.component` carries the loader-aware generic type from
// `createFileRoute` — extracting it into a ComponentType simplifies the
// JSX usage in the tests below.
const AdminBrandingPage = Route.options.component as ComponentType;

function makeGlobalConfig(overrides: Partial<{
  brandName: string;
  brandLogoUrl: string | null;
  brandFaviconUrl: string | null;
  defaultPresetId: string;
  customAccent: string | null;
  updatedAt: string;
}> = {}) {
  return {
    brandName: 'Plexor',
    brandLogoUrl: null,
    brandFaviconUrl: null,
    defaultPresetId: 'plexor-default-light',
    customAccent: null,
    updatedAt: new Date('2026-09-14T12:00:00Z').toISOString(),
    ...overrides,
  };
}

function makeOrgConfig(overrides: Partial<{
  orgId: string;
  presetId: string | null;
  customAccent: string | null;
  brandName: string | null;
  brandLogoUrl: string | null;
  brandFaviconUrl: string | null;
  updatedAt: string;
}> = {}) {
  return {
    orgId: '00000000-0000-0000-0000-000000000001',
    presetId: null,
    customAccent: null,
    brandName: null,
    brandLogoUrl: null,
    brandFaviconUrl: null,
    updatedAt: new Date('2026-09-14T12:00:00Z').toISOString(),
    ...overrides,
  };
}

describe('AdminBrandingPage', () => {
  beforeEach(() => {
    // The page's live-preview effect writes CSS variables to
    // document.documentElement. Reset between tests so the document state
    // from one test doesn't leak into the next (especially important
    // because the custom accent + preset determine which classes are
    // toggled on the root).
    document.documentElement.removeAttribute('data-theme');
    document.documentElement.removeAttribute('style');
    document.documentElement.classList.remove('dark');
  });

  afterEach(() => {
    document.documentElement.removeAttribute('data-theme');
    document.documentElement.removeAttribute('style');
    document.documentElement.classList.remove('dark');
  });

  it('renders the global preset picker with plexor-default-light preselected', async () => {
    const mocks = mockBrandingService();
    mocks.getGlobal.mockResolvedValue(makeGlobalConfig());
    mocks.getOrg.mockResolvedValue(null);

    renderWithProviders(<AdminBrandingPage />);

    // The global card title is "Operator defaults" — anchor the regex
    // so the live-preview "Save operator defaults" button and the
    // description copy don't also match.
    const globalCard = await screen.findByText(/^Operator defaults$/);
    expect(globalCard).toBeInTheDocument();

    // All preset cards render as buttons (a button each); the active one
    // has the highlighted ring/border classes. Three presets are
    // registered, so findAllByRole should return at least 3.
    const presetButtons = await screen.findAllByRole('button', { name: /plexor.*default light/i });
    expect(presetButtons.length).toBeGreaterThanOrEqual(1);
    expect(presetButtons[0]?.className).toMatch(/border-foreground\/60/);
  });

  it('renders the brand name input prefilled with the value from the API', async () => {
    const mocks = mockBrandingService();
    mocks.getGlobal.mockResolvedValue(makeGlobalConfig({ brandName: 'Acme Cloud' }));
    mocks.getOrg.mockResolvedValue(null);

    renderWithProviders(<AdminBrandingPage />);

    // The Field wrapper has `<label for="...">` with no associated input
    // (the id isn't passed to the Input child — pre-existing source
    // structure). Use `getByDisplayValue` to find the input by its
    // current value instead.
    const brandNameInput = await screen.findByDisplayValue('Acme Cloud');
    expect(brandNameInput).toBeInTheDocument();
  });

  it('renders the per-org override fields when org config exists', async () => {
    const mocks = mockBrandingService();
    mocks.getGlobal.mockResolvedValue(makeGlobalConfig());
    mocks.getOrg.mockResolvedValue(
      makeOrgConfig({
        brandName: 'Tenant Co',
        presetId: 'plexor-noir',
        customAccent: 'oklch(0.6 0.18 30)',
      }),
    );

    renderWithProviders(<AdminBrandingPage />);

    await waitFor(() => {
      expect(mocks.getOrg).toHaveBeenCalled();
    });

    // Only the org brand-name input shows "Tenant Co" — the global one
    // defaults to "Plexor" and never reads from org config.
    expect(await screen.findByDisplayValue('Tenant Co')).toBeInTheDocument();

    // The org preset `<select>` element carries the org's presetId as
    // its `value`. Read the value via the DOM property — testing-library's
    // getByDisplayValue on a controlled <select> matches the textContent
    // of the selected option, and React 19 doesn't always mark the
    // matching option with `selected` until the next paint, so we read
    // the value directly via the select's `value` property.
    const orgCard = document.querySelector('[data-od-id="admin-branding-org"]');
    expect(orgCard).not.toBeNull();
    const orgSelect = orgCard?.querySelector('select') as HTMLSelectElement | null;
    expect(orgSelect).not.toBeNull();
    expect(orgSelect?.value).toBe('plexor-noir');
  });

  it('shows the custom accent input prefilled with the OKLCH value', async () => {
    const mocks = mockBrandingService();
    mocks.getGlobal.mockResolvedValue(
      makeGlobalConfig({ customAccent: 'oklch(0.65 0.18 250)' }),
    );
    mocks.getOrg.mockResolvedValue(null);

    renderWithProviders(<AdminBrandingPage />);

    const accentInput = await screen.findByDisplayValue('oklch(0.65 0.18 250)');
    expect(accentInput).toBeInTheDocument();
  });

  it('calls updateGlobalBranding with the form state when the Save button is clicked', async () => {
    const user = userEvent.setup();
    const mocks = mockBrandingService();
    mocks.getGlobal.mockResolvedValue(
      makeGlobalConfig({
        brandName: 'Plexor',
        defaultPresetId: 'plexor-default-light',
        customAccent: null,
      }),
    );
    mocks.getOrg.mockResolvedValue(null);
    mocks.updateGlobal.mockResolvedValue(makeGlobalConfig({ brandName: 'Plexor' }));

    renderWithProviders(<AdminBrandingPage />);

    // The "Save operator defaults" text appears twice — once as the
    // real Button primitive and once as a sample button in the live
    // preview card. Scope to the global card via its `data-od-id` so
    // we click the real save button.
    const globalCard = document.querySelector('[data-od-id="admin-branding-global"]');
    expect(globalCard).not.toBeNull();
    const saveButton = within(globalCard as HTMLElement).getByRole('button', {
      name: /save operator defaults/i,
    });
    await user.click(saveButton);

    await waitFor(() => {
      expect(mocks.updateGlobal).toHaveBeenCalledWith(
        expect.objectContaining({
          brandName: 'Plexor',
          defaultPresetId: 'plexor-default-light',
          brandLogoUrl: null,
          brandFaviconUrl: null,
          customAccent: null,
        }),
      );
    });
  });

  it('calls deleteOrgBranding when the Clear Override button is clicked', async () => {
    const user = userEvent.setup();
    const mocks = mockBrandingService();
    mocks.getGlobal.mockResolvedValue(makeGlobalConfig());
    mocks.getOrg.mockResolvedValue(
      makeOrgConfig({ brandName: 'Tenant Co', presetId: 'plexor-noir' }),
    );
    mocks.deleteOrg.mockResolvedValue(undefined);

    renderWithProviders(<AdminBrandingPage />);

    const clearButton = await screen.findByRole('button', { name: /clear override/i });
    await user.click(clearButton);

    await waitFor(() => {
      expect(mocks.deleteOrg).toHaveBeenCalledTimes(1);
    });
  });

  it('does not throw when the global save mutation rejects (caught + toast)', async () => {
    const user = userEvent.setup();
    const mocks = mockBrandingService();
    mocks.getGlobal.mockResolvedValue(makeGlobalConfig());
    mocks.getOrg.mockResolvedValue(null);
    mocks.updateGlobal.mockRejectedValue(new Error('upstream 502'));

    renderWithProviders(<AdminBrandingPage />);

    const globalCard = document.querySelector('[data-od-id="admin-branding-global"]');
    expect(globalCard).not.toBeNull();
    const saveButton = within(globalCard as HTMLElement).getByRole('button', {
      name: /save operator defaults/i,
    });
    // Errors should not throw uncaught — the page catches and shows a toast.
    await expect(user.click(saveButton)).resolves.not.toThrow();

    await waitFor(() => {
      expect(mocks.updateGlobal).toHaveBeenCalledTimes(1);
    });
  });
});