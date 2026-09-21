import type { ReactNode } from 'react';
import { buttonVariants } from 'fumadocs-ui/components/ui/button';
import type { Icon } from '@nine-thirty-five/material-symbols-react';
import {
  ArrowOutward,
  AvTimer,
  Block,
  Check,
  Help,
} from '@nine-thirty-five/material-symbols-react/rounded/700';

/**
 * Landing hero for the docs index page. A component (not raw JSX in MDX) so
 * MDX cannot wrap the button labels in <p> — that broke text color
 * inheritance and made the primary button look blank.
 */
export function DocsHero({
  mark,
  title,
  subtitle,
  primaryLabel,
  primaryHref,
  secondaryLabel,
  secondaryHref,
}: {
  mark: ReactNode;
  title: string;
  subtitle: string;
  primaryLabel: string;
  primaryHref: string;
  secondaryLabel: string;
  secondaryHref: string;
}): ReactNode {
  return (
    <div className="not-prose my-10 flex flex-col items-center gap-4 text-center">
      {mark}
      <h1 className="mb-0 text-4xl font-semibold tracking-tight">{title}</h1>
      <div className="m-0 max-w-xl text-fd-muted-foreground">{subtitle}</div>
      <div className="mt-2 flex flex-wrap items-center justify-center gap-3">
        <a href={primaryHref} className={buttonVariants({ variant: 'primary' })}>
          {primaryLabel}
        </a>
        <a href={secondaryHref} className={buttonVariants({ variant: 'outline' })}>
          {secondaryLabel}
          <ArrowOutward className="size-4" aria-hidden />
        </a>
      </div>
    </div>
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
  const Icon: Icon | undefined =
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
      ? 'bg-ok-soft text-ok-ink border-ok/25'
      : tone === 'error'
        ? 'bg-err-soft text-err-ink border-err/25'
        : tone === 'pending'
          ? 'bg-warn-soft text-warn-ink border-warn/25'
          : 'bg-fd-muted text-fd-muted-foreground border-fd-border';

  return (
    <span
      className={`inline-flex items-center gap-1.5 whitespace-nowrap rounded-full border px-2.5 py-0.5 text-[0.85em] font-medium ${toneClass}`}
    >
      {Icon !== undefined ? <Icon className="size-3.5 shrink-0" aria-hidden /> : null}
      {label}
    </span>
  );
}