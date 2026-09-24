import { useEffect, useState } from 'react';

const QUERY = '(prefers-reduced-motion: reduce)';

function readPrefersReducedMotion(): boolean {
  if (typeof window === 'undefined' || typeof window.matchMedia !== 'function') return false;
  return window.matchMedia(QUERY).matches;
}

/**
 * Reactive `prefers-reduced-motion` flag, always a `boolean` (never `null`)
 * so every consumer can branch without an extra guard.
 *
 * `motion/react`'s own `useReducedMotion()` snapshots the media query once
 * via `useState(prefersReducedMotion.current)` and never re-renders on
 * change (see `framer-motion/dist/es/utils/reduced-motion/use-reduced-motion.mjs`
 * — its own source comment flags this as a known gap). This hook attaches
 * a live `change` listener instead, so a component degrades to static
 * immediately if the OS setting flips while the page is open — the
 * behaviour every motion component in this folder is required to have.
 */
export function useReducedMotionSafe(): boolean {
  const [reduced, setReduced] = useState<boolean>(readPrefersReducedMotion);

  useEffect(() => {
    if (typeof window === 'undefined' || typeof window.matchMedia !== 'function') return;
    const mql = window.matchMedia(QUERY);
    const onChange = () => setReduced(mql.matches);
    onChange();
    mql.addEventListener('change', onChange);
    return () => mql.removeEventListener('change', onChange);
  }, []);

  return reduced;
}
