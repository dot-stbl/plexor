import { useEffect, useState } from 'react';

/**
 * Milliseconds elapsed since `active` last flipped to `true`, driven by
 * `requestAnimationFrame`. Resets to 0 (and stops the loop) the instant
 * `active` goes false — so a caller can gate this on `inView &&
 * !reducedMotion` and get a clean restart-from-zero for free.
 *
 * Stops scheduling further frames once the elapsed time reaches `maxMs`
 * (holding the final render at exactly `maxMs`) rather than looping
 * forever — the typing/count-up effects this feeds are one-shot, not
 * continuous, so there is nothing left to animate past that point.
 */
export function useElapsedMs(active: boolean, maxMs: number): number {
  const [elapsed, setElapsed] = useState(0);

  useEffect(() => {
    if (!active) {
      setElapsed(0);
      return;
    }

    let start: number | null = null;
    let frameId = 0;

    const tick = (time: number) => {
      if (start === null) start = time;
      const delta = time - start;
      const clamped = Math.min(delta, maxMs);
      setElapsed(clamped);
      if (delta < maxMs) frameId = requestAnimationFrame(tick);
    };

    frameId = requestAnimationFrame(tick);
    return () => cancelAnimationFrame(frameId);
  }, [active, maxMs]);

  return elapsed;
}
