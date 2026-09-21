import type { ComponentType, ReactNode } from 'react';
import { buttonVariants } from 'fumadocs-ui/components/ui/button';
import { ArrowOutward, Check, Block, AvTimer, Help } from '@nine-thirty-five/material-symbols-react/rounded/700';
import { TerminalPlayground } from '@/components/terminal-playground';

/**
 * Landing hero. The Plexor headline + tagline + two CTAs, with a `Hello,
 * plx` terminal animation sitting underneath. Hero lives only on the
 * landing page — every other page uses a quieter first-page header
 * (the section landing pages render their own intro through `index.mdx`).
 *
 * Layout: left column holds the editorial headline + CTAs; right column
 * is the animated terminal. On small screens they stack. No background
 * illustrations, no glow — typography + a live command block is the
 * entire above-the-fold.
 */
export function DocsHero({
  kicker,
  title,
  subtitle,
  primaryLabel,
  primaryHref,
  secondaryLabel,
  secondaryHref,
}: {
  kicker?: string;
  title: string;
  subtitle: string;
  primaryLabel: string;
  primaryHref: string;
  secondaryLabel: string;
  secondaryHref: string;
}): ReactNode {
  return (
    <header className="not-prose relative grid grid-cols-1 items-start gap-10 py-8 lg:grid-cols-[1.05fr_1fr]">
      <div className="flex flex-col gap-6">
        {kicker === undefined ? null : (
          <p className="text-fd-muted-foreground m-0 text-xs font-medium uppercase tracking-[0.22em]">
            {kicker}
          </p>
        )}
        <h1 className="m-0 text-4xl leading-[1.05] font-semibold tracking-[-0.02em] sm:text-5xl">
          {title}
        </h1>
        <p className="text-fd-muted-foreground m-0 max-w-xl text-lg leading-relaxed">
          {subtitle}
        </p>
        <div className="flex flex-wrap items-center gap-3">
          <a href={primaryHref} className={buttonVariants({ variant: 'primary' })}>
            {primaryLabel}
          </a>
          <a
            href={secondaryHref}
            className={buttonVariants({ variant: 'ghost' })}
          >
            {secondaryLabel}
            <ArrowOutward className="size-4" aria-hidden />
          </a>
        </div>
        <div className="text-fd-muted-foreground flex items-center gap-4 text-xs">
          <span className="inline-flex items-center gap-1.5">
            <Check className="text-fd-ok size-3.5" aria-hidden />
            MIT
          </span>
          <span className="inline-flex items-center gap-1.5">
            <Check className="text-fd-ok size-3.5" aria-hidden />
            Apache-2.0 (docs)
          </span>
          <span className="inline-flex items-center gap-1.5">
            <AvTimer className="text-fd-muted-foreground size-3.5" aria-hidden />
            v0.x · early
          </span>
        </div>
      </div>
      <TerminalPlayground />
    </header>
  );
}

/**
 * Status pill — Plexor DS status semantics. Tones map to the `--ok` /
 * `--warn` / `--err` / `--idle` / `--info` tokens from console (OKLCH).
 * `icon` carries the semantics (ok / error / pending / neutral); no extra
 * colour is added — the background already encodes the tone.
 */
export function Status({
  label,
  tone,
  icon,
}: {
  label: string;
  tone: 'ok' | 'error' | 'pending' | 'neutral';
  icon?: 'check' | 'ban' | 'clock' | 'help';
}): ReactNode {
  const Icon: ComponentType<{ className?: string }> | undefined =
    icon === 'check'
      ? Check
      : icon === 'ban'
        ? Block
        : icon === 'clock'
          ? AvTimer
          : icon === 'help'
            ? Help
            : undefined;

  const toneClass =
    tone === 'ok'
      ? 'bg-fd-ok-soft text-fd-ok-ink border-fd-ok/30'
      : tone === 'error'
        ? 'bg-fd-err-soft text-fd-err-ink border-fd-err/30'
        : tone === 'pending'
          ? 'bg-fd-warn-soft text-fd-warn-ink border-fd-warn/30'
          : 'bg-fd-muted text-fd-muted-foreground border-fd-border';

  return (
    <span
      className={`inline-flex items-center gap-1.5 whitespace-nowrap rounded-full border px-2.5 py-0.5 text-[0.85em] font-medium ${toneClass}`}
    >
      {Icon !== undefined ? (
        <Icon className="size-3.5 shrink-0" aria-hidden />
      ) : null}
      {label}
    </span>
  );
}