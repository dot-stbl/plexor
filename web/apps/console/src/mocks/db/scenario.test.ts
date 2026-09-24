import { afterEach, describe, expect, it } from 'vitest';
import { getMockScenario } from './scenario';

function setSearch(search: string): void {
  window.history.pushState({}, '', `/vms${search}`);
}

afterEach(() => {
  window.history.pushState({}, '', '/');
  window.localStorage.removeItem('plexor:mock-scenario');
});

describe('getMockScenario', () => {
  it('defaults to "default" with no query param or localStorage flag', () => {
    expect(getMockScenario()).toBe('default');
  });

  it('reads the ?mock= query param', () => {
    setSearch('?mock=empty');
    expect(getMockScenario()).toBe('empty');
  });

  it('falls back to the localStorage flag when no query param is set', () => {
    window.localStorage.setItem('plexor:mock-scenario', 'slow');
    expect(getMockScenario()).toBe('slow');
  });

  it('ignores an invalid value and falls back to "default"', () => {
    setSearch('?mock=bogus');
    expect(getMockScenario()).toBe('default');
  });

  it('the query param wins over localStorage when both are set', () => {
    window.localStorage.setItem('plexor:mock-scenario', 'slow');
    setSearch('?mock=error');
    expect(getMockScenario()).toBe('error');
  });
});