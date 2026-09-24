import type { ReactNode } from 'react';
import { motion } from 'motion/react';
import { cn } from '@/lib/utils';
import { useReducedMotionSafe } from './use-reduced-motion-safe';

export interface RevealProps {
  readonly children: ReactNode;
  readonly className?: string;
  /** Upward travel distance in px. Default 16. */
  readonly y?: number;
  /** Seconds before the animation starts, once in view. Default 0. */
  readonly delay?: number;
  /** Animation duration in seconds. Default 0.5. */
  readonly duration?: number;
  /** Fraction of the element that must be visible to trigger. Default 0.3. */
  readonly amount?: number;
}

/**
 * Fade a block in as it enters the viewport: opacity 0→1 plus a small
 * upward `y` travel, no blur (blur filters are expensive to composite
 * and this repo's motion vocabulary is transform/opacity only). Fires
 * once — `viewport={{ once: true }}` — then leaves the element alone,
 * so re-scrolling past it never re-triggers the animation.
 *
 * Degrades to a plain, already-visible `<div>` under reduced motion.
 */
export function Reveal({ children, className, y = 16, delay = 0, duration = 0.5, amount = 0.3 }: RevealProps) {
  const reducedMotion = useReducedMotionSafe();

  if (reducedMotion) {
    return <div className={className}>{children}</div>;
  }

  return (
    <motion.div
      className={cn(className)}
      initial={{ opacity: 0, y }}
      whileInView={{ opacity: 1, y: 0 }}
      viewport={{ once: true, amount }}
      transition={{ duration, delay, ease: 'easeOut' }}
    >
      {children}
    </motion.div>
  );
}
