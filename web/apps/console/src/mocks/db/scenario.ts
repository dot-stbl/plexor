/**
 * Mock scenario switch — lets a dev/demo/test force a specific mock
 * behavior without editing fixtures. Read once per call (cheap), so a
 * page navigation (which changes the URL) picks up a new value
 * immediately; a manual `localStorage` edit needs a reload.
 *
 * Usage: `http://localhost:5173/vms?mock=empty`, or in devtools:
 * `localStorage.setItem('plexor:mock-scenario', 'slow')`.
 */
export type MockScenario = 'default' | 'empty' | 'error' | 'slow';

const SCENARIOS: readonly MockScenario[] = ['default', 'empty', 'error', 'slow'];

function isMockScenario(value: string | null): value is MockScenario {
  return value !== null && (SCENARIOS as readonly string[]).includes(value);
}

/** Resolve the active scenario: `?mock=` query param wins, then the
 *  `plexor:mock-scenario` localStorage key, else `'default'`. Safe to
 *  call outside a browser (returns `'default'`) — msw/node smoke tests
 *  and SSR-less unit tests never set a scenario. */
export function getMockScenario(): MockScenario {
  if (typeof window === 'undefined') return 'default';
  try {
    const fromQuery = new URLSearchParams(window.location.search).get('mock');
    if (isMockScenario(fromQuery)) return fromQuery;
    const fromStorage = window.localStorage.getItem('plexor:mock-scenario');
    if (isMockScenario(fromStorage)) return fromStorage;
  } catch {
    // localStorage can throw in a locked-down/private context — fall through.
  }
  return 'default';
}
