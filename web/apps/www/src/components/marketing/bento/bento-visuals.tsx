import { useEffect, useState } from 'react';
import { StatusPill } from '@/components/ui/status-pill';
import { Stagger, StaggerItem, useReducedMotionSafe } from '@/components/motion';
import { countUp, typedLength } from '../timed-reveal';
import { useElapsedMs } from '../use-elapsed-ms';
import { useInViewOnce } from '../use-in-view-once';

/**
 * Small, decorative, token-built visuals for the bento grid (amendment
 * A3) — one per cell, all `aria-hidden` (they carry no information the
 * cell's title/body text doesn't already say). Plexor DS tokens only —
 * no hex, no gradients.
 *
 * Four of the six go "live" once scrolled into view (brief: status dots
 * pending→running, quota bar count-up, audit lines appending, catalog
 * terminal typing) — each a one-shot flow that carries meaning (a real
 * provisioning/typing moment), not a decorative repeating loop. `chip-
 * row` and `scope-path` stay static — nothing about a network CIDR list
 * or an org/team/folder path "happens" over time. `useInViewOnce`
 * already resolves to `true` immediately under reduced motion, so every
 * effect below renders its finished state on first paint in that case.
 */

export function VisualStatusList() {
  const [ref, inView] = useInViewOnce<HTMLDivElement>();
  const reducedMotion = useReducedMotionSafe();
  const [provisioned, setProvisioned] = useState(reducedMotion);

  // Live OS-setting flips still land on the "arrived" state immediately
  // (same contract `useReducedMotionSafe` documents for itself), not just
  // the initial render.
  useEffect(() => {
    if (reducedMotion) setProvisioned(true);
  }, [reducedMotion]);

  useEffect(() => {
    if (!inView || reducedMotion || provisioned) return;
    const timer = setTimeout(() => setProvisioned(true), 900);
    return () => clearTimeout(timer);
  }, [inView, reducedMotion, provisioned]);

  const rows: readonly { name: string; variant: 'running' | 'pending' }[] = [
    { name: 'web-01', variant: 'running' },
    { name: 'db-primary', variant: 'running' },
    { name: 'cache-02', variant: provisioned ? 'running' : 'pending' },
  ];

  return (
    <div ref={ref} aria-hidden className="mt-4 space-y-1.5">
      {rows.map((row) => (
        <div
          key={row.name}
          className="flex items-center justify-between rounded-md border border-border bg-background px-2 py-1"
        >
          <span className="font-mono text-[11px] text-muted-foreground">{row.name}</span>
          <StatusPill variant={row.variant} size="sm" />
        </div>
      ))}
    </div>
  );
}

export function VisualChipRow() {
  const chips: readonly string[] = ['10.0.4.0/24', 'fip · 203.0.113.9', 'lb · round-robin'];
  return (
    <div aria-hidden className="mt-4 flex flex-wrap gap-1.5">
      {chips.map((chip) => (
        <span
          key={chip}
          className="rounded-full border border-border bg-background px-2 py-0.5 font-mono text-[10px] text-muted-foreground"
        >
          {chip}
        </span>
      ))}
    </div>
  );
}

const CAPACITY_TARGET_GB = 312;
const CAPACITY_TOTAL_GB = 512;
const CAPACITY_DURATION_MS = 900;

export function VisualCapacityBar() {
  const [ref, inView] = useInViewOnce<HTMLDivElement>();
  const reducedMotion = useReducedMotionSafe();
  const elapsed = useElapsedMs(inView && !reducedMotion, CAPACITY_DURATION_MS);
  const value = reducedMotion ? CAPACITY_TARGET_GB : countUp(CAPACITY_TARGET_GB, elapsed, CAPACITY_DURATION_MS);

  return (
    <div ref={ref} aria-hidden className="mt-4">
      <div className="h-1.5 w-full overflow-hidden rounded-full bg-background">
        {/* `transform: scaleX` (not `width`) — transform/opacity only, per the motion brief. */}
        <div
          className="h-full w-full origin-left rounded-full bg-foreground/60"
          style={{ transform: `scaleX(${value / CAPACITY_TOTAL_GB})` }}
        />
      </div>
      <p className="mt-1.5 font-mono text-[10px] text-muted-2 tabular-nums">
        {value} GB / {CAPACITY_TOTAL_GB} GB
      </p>
    </div>
  );
}

export function VisualScopePath() {
  return (
    <div
      aria-hidden
      className="mt-4 flex items-center gap-1.5 rounded-md border border-border bg-background px-2 py-1.5 font-mono text-[11px] text-muted-foreground"
    >
      <span>acme-corp</span>
      <span className="text-muted-2">›</span>
      <span>platform</span>
      <span className="text-muted-2">›</span>
      <span className="text-foreground">prod</span>
    </div>
  );
}

const LOG_ROWS: readonly string[] = ['10:42 · vm.create', '10:44 · volume.attach', '10:51 · role.grant'];

/** Audit lines "appending" — a `Stagger` reveal (already viewport-triggered, already reduced-motion-safe). */
export function VisualLogList() {
  return (
    <Stagger className="mt-4 space-y-1" staggerChildren={0.25} amount={0.6}>
      {LOG_ROWS.map((row) => (
        <StaggerItem key={row}>
          <p aria-hidden className="font-mono text-[10px] text-muted-2">
            {row}
          </p>
        </StaggerItem>
      ))}
    </Stagger>
  );
}

const COMMAND_TEXT = '$ plexor install postgres';
const COMMAND_MS_PER_CHAR = 45;

export function VisualCommandLine() {
  const [ref, inView] = useInViewOnce<HTMLDivElement>();
  const reducedMotion = useReducedMotionSafe();
  const elapsed = useElapsedMs(inView && !reducedMotion, COMMAND_TEXT.length * COMMAND_MS_PER_CHAR);
  const shown = reducedMotion
    ? COMMAND_TEXT
    : COMMAND_TEXT.slice(0, typedLength(COMMAND_TEXT.length, elapsed, COMMAND_MS_PER_CHAR));
  const typing = !reducedMotion && shown.length < COMMAND_TEXT.length;

  return (
    <div
      ref={ref}
      aria-hidden
      className="mt-4 rounded-md border border-border bg-background px-2 py-1.5 font-mono text-[11px] text-muted-foreground"
    >
      {shown}
      {typing && <span className="animate-pulse">▍</span>}
    </div>
  );
}
