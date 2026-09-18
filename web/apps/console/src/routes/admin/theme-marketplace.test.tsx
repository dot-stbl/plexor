/**
 * AdminThemeMarketplacePage component tests — the marketplace UI's
 * grid + Activate action. v1 ships community themes from a local
 * bundle, so the page's data hook is synchronous; Activate writes to
 * localStorage so the next reload picks up the choice. The tests
 * cover four paths: rendering, active-state highlighting, and the
 * persistence side-effect of clicking Activate.
 *
 * Selectors: this codebase uses `data-od-id` (operator-defined id) on
 * containers rather than `data-testid`, so DOM lookups here go through
 * `document.querySelector` instead of testing-library's
 * `getByTestId`. Asserting text content + role lookups inside the
 * queried subtree uses `within` for normal queries.
 */
import { afterEach, beforeEach, describe, expect, it } from 'vitest';
import type { ComponentType } from 'react';
import { screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { Route } from './theme-marketplace';
import { renderWithProviders } from '@/test-utils';

const AdminThemeMarketplacePage = Route.options.component as ComponentType;

const STORAGE_KEY = 'plexor.theme.activation';

function findCard(dataOdId: string): HTMLElement | null {
  return document.querySelector(`[data-od-id="${dataOdId}"]`);
}

describe('AdminThemeMarketplacePage', () => {
  beforeEach(() => {
    // Reset document + localStorage between tests — the page reads
    // both on first paint and on Activate.
    document.documentElement.removeAttribute('data-theme');
    document.documentElement.removeAttribute('style');
    document.documentElement.classList.remove('dark');
    window.localStorage.clear();
  });

  afterEach(() => {
    document.documentElement.removeAttribute('data-theme');
    document.documentElement.removeAttribute('style');
    document.documentElement.classList.remove('dark');
    window.localStorage.clear();
  });

  it('renders the community grid with both community themes', async () => {
    renderWithProviders(<AdminThemeMarketplacePage />);

    // Each card is rendered once the queryFn resolves (microtask).
    // waitFor each card so the assertion order matches when DOM is
    // ready.
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
    renderWithProviders(<AdminThemeMarketplacePage />);

    // Built-in section is a sibling of the community grid; matching
    // by section title keeps the assertion independent of card count
    // changes.
    await screen.findByText(/^Built-in$/);
    expect(findCard('admin-theme-marketplace-built-in')).not.toBeNull();
  });

  it('highlights the active community theme when one is persisted', async () => {
    window.localStorage.setItem(STORAGE_KEY, 'paper-light');

    renderWithProviders(<AdminThemeMarketplacePage />);

    await waitFor(() => {
      expect(findCard('theme-marketplace-card-paper-light')).not.toBeNull();
    });
    const card = findCard('theme-marketplace-card-paper-light') as HTMLElement;
    // The "Active" badge appears twice on the active card (header +
    // button label); the inactive cards have only one ("Activate").
    expect(within(card).getAllByText(/Active/i).length).toBeGreaterThanOrEqual(2);
  });

  it('clicking Activate persists to localStorage and updates the active highlight', async () => {
    const user = userEvent.setup();

    renderWithProviders(<AdminThemeMarketplacePage />);

    await waitFor(() => {
      expect(findCard('theme-marketplace-card-synthwave-night-dark')).not.toBeNull();
    });
    const card = findCard('theme-marketplace-card-synthwave-night-dark') as HTMLElement;
    const activateButton = within(card).getByRole('button', { name: /Activate/i });
    await user.click(activateButton);

    // localStorage is the persistence target.
    await waitFor(() => {
      expect(window.localStorage.getItem(STORAGE_KEY)).toBe('synthwave-night-dark');
    });

    // The clicked card now shows the Active badge (twice: header + button).
    await waitFor(() => {
      const updated = findCard('theme-marketplace-card-synthwave-night-dark') as HTMLElement;
      expect(within(updated).getAllByText(/Active/i).length).toBeGreaterThanOrEqual(2);
    });
  });
});
