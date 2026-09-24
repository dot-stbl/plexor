import { useRef, type RefObject } from 'react';
import { useInView } from 'motion/react';
import { useReducedMotionSafe } from '@/components/motion';

/**
 * A ref to attach to the element to watch, plus a boolean that flips to
 * `true` once that element has entered the viewport and stays `true`
 * (`once: true`) — and is `true` from the very first render under
 * reduced motion, without ever attaching an `IntersectionObserver`. Every
 * "live when in view" bento visual and the "Get running" terminal read
 * this instead of wiring their own observer, so "reduced motion → show
 * the end state immediately" is handled in exactly one place.
 */
export function useInViewOnce<T extends Element>(amount = 0.4): readonly [RefObject<T | null>, boolean] {
  const ref = useRef<T>(null);
  const reducedMotion = useReducedMotionSafe();
  const inView = useInView(ref, { once: true, amount });
  return [ref, reducedMotion || inView] as const;
}
