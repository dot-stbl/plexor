import type { ReactNode } from 'react';
import { motion } from 'motion/react';
import { cn } from '@/lib/utils';
import { useReducedMotionSafe } from './use-reduced-motion-safe';

export interface StaggerProps {
  readonly children: ReactNode;
  readonly className?: string;
  /** Seconds between each child's animation start. Default 0.08. */
  readonly staggerChildren?: number;
  /** Fraction of the container that must be visible to trigger. Default 0.2. */
  readonly amount?: number;
}

/**
 * Orchestrates a group of `<StaggerItem>` children: fires once the
 * container enters the viewport, then plays each child's `show` variant
 * `staggerChildren` seconds apart — `motion`'s built-in variant
 * propagation, no context wiring needed as long as `StaggerItem` is a
 * (possibly nested) child of this element.
 *
 * Degrades to a plain, already-visible `<div>` under reduced motion —
 * `StaggerItem` does the same check independently, so it renders static
 * even if ever used outside a `<Stagger>`.
 */
export function Stagger({ children, className, staggerChildren = 0.08, amount = 0.2 }: StaggerProps) {
  const reducedMotion = useReducedMotionSafe();

  if (reducedMotion) {
    return <div className={className}>{children}</div>;
  }

  // Built per-render (not hoisted) so `staggerChildren` is captured
  // directly — simpler and less surprising than relying on `motion`'s
  // `custom` prop to thread a dynamic-variant argument down to children.
  const containerVariants = {
    hidden: {},
    show: { transition: { staggerChildren, delayChildren: 0 } },
  };

  return (
    <motion.div
      className={cn(className)}
      initial="hidden"
      whileInView="show"
      viewport={{ once: true, amount }}
      variants={containerVariants}
    >
      {children}
    </motion.div>
  );
}

const ITEM_VARIANTS = {
  hidden: { opacity: 0, y: 12 },
  show: { opacity: 1, y: 0 },
};

export interface StaggerItemProps {
  readonly children: ReactNode;
  readonly className?: string;
  /** Animation duration in seconds. Default 0.4. */
  readonly duration?: number;
}

/** One animated child of a `<Stagger>`. See `Stagger` for orchestration. */
export function StaggerItem({ children, className, duration = 0.4 }: StaggerItemProps) {
  const reducedMotion = useReducedMotionSafe();

  if (reducedMotion) {
    return <div className={className}>{children}</div>;
  }

  return (
    <motion.div className={cn(className)} variants={ITEM_VARIANTS} transition={{ duration, ease: 'easeOut' }}>
      {children}
    </motion.div>
  );
}
