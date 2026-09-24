/**
 * Seeded artificial latency for MSW handlers — makes the mock feel like
 * a real network instead of resolving instantly. Deterministic jitter
 * (not `Math.random()`) so repeated runs are stable; zero under Vitest
 * (`import.meta.env.VITEST`) so the test suite stays fast; scaled up
 * under the `slow` mock scenario (see `./scenario`).
 */
import { getMockScenario } from './scenario';

let counter = 0;

/** Small deterministic pseudo-random float in [0, 1), advancing on every call. */
function nextJitter(): number {
  counter += 1;
  const x = Math.sin(counter * 12.9898) * 43758.5453;
  return x - Math.floor(x);
}

/**
 * Resolve after a small, seeded delay. `baseMs` is the center of the
 * jittered range (±40%). Returns immediately under Vitest. Multiplies
 * the delay ~6x under the `slow` scenario.
 */
export async function mockDelay(baseMs = 250): Promise<void> {
  if (import.meta.env.VITEST) return;
  const scenario = getMockScenario();
  const scale = scenario === 'slow' ? 6 : 1;
  const jitter = 0.6 + nextJitter() * 0.8; // 0.6x .. 1.4x
  const ms = Math.round(baseMs * scale * jitter);
  await new Promise((resolve) => setTimeout(resolve, ms));
}
