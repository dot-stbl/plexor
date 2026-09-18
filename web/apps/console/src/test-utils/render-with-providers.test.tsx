/**
 * renderWithProviders smoke test — the helper's contract is "every test in
 * this app imports from here", so we prove the wiring is end-to-end correct
 * (Query + Router + i18n + Preferences) before the first real component
 * test lands on top of it.
 */
import { describe, expect, it } from 'vitest';
import { renderWithProviders } from './render-with-providers';

describe('renderWithProviders', () => {
  it('renders the element inside all four providers', () => {
    const result = renderWithProviders(<div data-testid="smoke">hello</div>);

    expect(result.getByTestId('smoke')).toHaveTextContent('hello');
  });
});