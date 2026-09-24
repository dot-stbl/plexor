/**
 * Argument parsing for `shot page` — positional targets (paths) + flags
 * (`--theme|--mobile|--full|--motion|--width`) plus interaction steps
 * (`--click|--hover|--fill|--press|--wait|--scroll`), run in the order
 * they appear in argv.
 *
 * Adapted from `web/apps/console/scripts/agent/lib/cli-args.ts`: added
 * `--motion` (emulates `prefers-reduced-motion: no-preference` instead
 * of the tool's `reduce` default — needed to verify this app's
 * `motion`-driven scroll/reveal effects, which console has none of),
 * `--scroll <px>` (a repeatable step, same shape as `--wait`, for
 * checking mid-animation states at specific scroll depths), and
 * `--width <px>` (override the desktop viewport's width — this app's
 * full-width layout needs checking at ultrawide breakpoints beyond the
 * tool's 1280px default; ignored together with `--mobile`, which picks
 * its own fixed viewport).
 */

import type { Step } from './steps';

export type ThemeArg = 'light' | 'dark' | 'both';

export interface ParsedShotArgs {
  readonly positionals: string[];
  readonly theme: ThemeArg;
  readonly mobile: boolean;
  readonly full: boolean;
  /** Emulate `prefers-reduced-motion: no-preference`. Default false (tool default stays `reduce`). */
  readonly motion: boolean;
  /** Desktop viewport width override in px. `undefined` keeps the tool's 1280px default. Ignored under `--mobile`. */
  readonly width: number | undefined;
  readonly steps: readonly Step[];
}

const VALUE_FLAGS = new Set(['--theme', '--width', '--click', '--hover', '--fill', '--press', '--wait', '--scroll']);
const BOOL_FLAGS = new Set(['--mobile', '--full', '--motion']);

export function parseShotArgs(argv: readonly string[]): ParsedShotArgs {
  const positionals: string[] = [];
  const steps: Step[] = [];
  let theme: ThemeArg = 'light';
  let mobile = false;
  let full = false;
  let motion = false;
  let width: number | undefined;

  for (let i = 0; i < argv.length; i++) {
    const arg = argv[i];
    if (!arg.startsWith('--')) {
      positionals.push(arg);
      continue;
    }
    if (BOOL_FLAGS.has(arg)) {
      if (arg === '--mobile') mobile = true;
      else if (arg === '--full') full = true;
      else motion = true;
      continue;
    }
    if (!VALUE_FLAGS.has(arg)) {
      throw new Error(`Unknown flag: ${arg}`);
    }
    i += 1;
    const value = argv[i];
    if (value === undefined) {
      throw new Error(`Flag ${arg} needs a value`);
    }
    applyValueFlag(arg, value, steps, (t) => (theme = t), (w) => (width = w));
  }

  return { positionals, theme, mobile, full, motion, width, steps };
}

function applyValueFlag(
  flag: string,
  value: string,
  steps: Step[],
  setTheme: (theme: ThemeArg) => void,
  setWidth: (width: number) => void,
): void {
  switch (flag) {
    case '--theme':
      if (value !== 'light' && value !== 'dark' && value !== 'both') {
        throw new Error(`--theme must be light|dark|both, got "${value}"`);
      }
      setTheme(value);
      return;
    case '--width': {
      const px = Number.parseInt(value, 10);
      if (!Number.isFinite(px) || px <= 0) {
        throw new Error(`--width needs a positive number of pixels, got "${value}"`);
      }
      setWidth(px);
      return;
    }
    case '--click':
      steps.push({ kind: 'click', selector: value });
      return;
    case '--hover':
      steps.push({ kind: 'hover', selector: value });
      return;
    case '--fill': {
      // <selector>=<value> — split on the LAST '=' since Playwright
      // selector engines themselves use '=' (`text=Foo`, `role=button[name="x"]`).
      const eq = value.lastIndexOf('=');
      if (eq < 0) {
        throw new Error(`--fill needs "<selector>=<value>", got "${value}"`);
      }
      steps.push({ kind: 'fill', selector: value.slice(0, eq), value: value.slice(eq + 1) });
      return;
    }
    case '--press':
      steps.push({ kind: 'press', key: value });
      return;
    case '--wait': {
      const ms = Number.parseInt(value, 10);
      if (!Number.isFinite(ms)) {
        throw new Error(`--wait needs a number of milliseconds, got "${value}"`);
      }
      steps.push({ kind: 'wait', ms });
      return;
    }
    case '--scroll': {
      const px = Number.parseInt(value, 10);
      if (!Number.isFinite(px)) {
        throw new Error(`--scroll needs a number of pixels, got "${value}"`);
      }
      steps.push({ kind: 'scroll', px });
      return;
    }
    default:
      throw new Error(`Unknown flag: ${flag}`);
  }
}

/** Expands `--theme both` into concrete runs. */
export function themesFor(theme: ThemeArg): readonly ('light' | 'dark')[] {
  return theme === 'both' ? ['light', 'dark'] : [theme];
}
