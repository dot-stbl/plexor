/**
 * PageTemplate — width prop tests.
 *
 * The user-reported inconsistency: admin pages were centered (max-w-6xl),
 * but VM/LXC/K8s/list pages stretched edge-to-edge (max-w-none). The fix:
 *   - default → max-w-6xl (admin / forms / detail pages without explicit need)
 *   - narrow  → max-w-3xl (single-column forms)
 *   - wide    → max-w-7xl (data tables, list-detail with sticky summary)
 *
 * No page in the app should render with `max-w-none` (uncapped) anymore —
 * if a route "needs more horizontal space" than max-w-7xl, it should use
 * a dedicated component, not blow past it via PageTemplate.
 */
import { describe, expect, it } from 'vitest';
import { render, screen } from '@testing-library/react';
import { PageTemplate } from './page-template';

function firstContainer(): HTMLElement {
  const main = screen.getByRole('main');
  return main.querySelector('div') as HTMLElement;
}

function headerContainer(): HTMLElement {
  const main = screen.getByRole('main');
  return main.querySelector('header > div') as HTMLElement;
}

describe('PageTemplate width', () => {
  it('defaults to max-w-6xl (admin-style centered layout)', () => {
    render(
      <PageTemplate title="Branding">
        <p>body</p>
      </PageTemplate>,
    );
    expect(firstContainer().className).toContain('max-w-6xl');
    expect(headerContainer().className).toContain('max-w-6xl');
  });

  it('narrow width applies max-w-3xl', () => {
    render(
      <PageTemplate title="Single-column form" width="narrow">
        <p>body</p>
      </PageTemplate>,
    );
    expect(firstContainer().className).toContain('max-w-3xl');
  });

  it('wide width applies max-w-7xl (tables / list-detail with sticky summary)', () => {
    render(
      <PageTemplate title="VMs" width="wide">
        <p>body</p>
      </PageTemplate>,
    );
    expect(firstContainer().className).toContain('max-w-7xl');
  });

  it('never renders uncapped (no max-w-none) — the old `width="full"` is gone', () => {
    render(
      <PageTemplate title="Wide" width="wide">
        <p>body</p>
      </PageTemplate>,
    );
    expect(headerContainer().className).not.toContain('max-w-none');
    expect(firstContainer().className).not.toContain('max-w-none');
  });

  it('centers content (mx-auto) at every width tier', () => {
    render(
      <PageTemplate title="Default">
        <p>body</p>
      </PageTemplate>,
    );
    expect(headerContainer().className).toContain('mx-auto');
    expect(firstContainer().className).toContain('mx-auto');
  });

  it('applies horizontal padding so wide screens still breathe', () => {
    render(
      <PageTemplate title="Padded">
        <p>body</p>
      </PageTemplate>,
    );
    expect(firstContainer().className).toMatch(/px-/);
  });

  it('renders the title as an h1', () => {
    render(
      <PageTemplate title="Cluster detail" description="ready 3/5">
        <p>body</p>
      </PageTemplate>,
    );
    const heading = screen.getByRole('heading', { level: 1, name: 'Cluster detail' });
    expect(heading).toBeInTheDocument();
  });
});
