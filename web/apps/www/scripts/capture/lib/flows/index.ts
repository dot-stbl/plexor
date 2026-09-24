import { heroFlow } from './hero';
import { catalogFlow } from './catalog';
import { auditFlow } from './audit';
import type { Flow } from './types';

export type { Flow } from './types';

/**
 * The 3 capture flows used by the landing's screenshots — see
 * `src/content/media-manifest.ts`, the hand-written list of what's
 * actually used. Order is the order `capture.ts` logs them in, nothing
 * more (there's no shared storyline any more — screenshots are
 * independent stills, not chapters of a scroll-scrubbed tour).
 */
export const FLOWS: readonly Flow[] = [heroFlow, catalogFlow, auditFlow];
