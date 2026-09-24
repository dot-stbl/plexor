/**
 * Разбор аргументов для `shot page` / `shot story` — позиционные таргеты
 * (пути/id) + флаги `--theme|--mobile|--full` + шаги взаимодействия
 * (`--click|--hover|--fill|--press|--wait`), выполняемые в порядке
 * появления в argv.
 */

import type { Step } from './steps';

export type ThemeArg = 'light' | 'dark' | 'both';

export interface ParsedShotArgs {
  readonly positionals: string[];
  readonly theme: ThemeArg;
  readonly mobile: boolean;
  readonly full: boolean;
  readonly steps: readonly Step[];
}

const VALUE_FLAGS = new Set(['--theme', '--click', '--hover', '--fill', '--press', '--wait']);
const BOOL_FLAGS = new Set(['--mobile', '--full']);

export function parseShotArgs(argv: readonly string[]): ParsedShotArgs {
  const positionals: string[] = [];
  const steps: Step[] = [];
  let theme: ThemeArg = 'light';
  let mobile = false;
  let full = false;

  for (let i = 0; i < argv.length; i++) {
    const arg = argv[i];
    if (!arg.startsWith('--')) {
      positionals.push(arg);
      continue;
    }
    if (BOOL_FLAGS.has(arg)) {
      if (arg === '--mobile') mobile = true;
      else full = true;
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
    applyValueFlag(arg, value, steps, (t) => (theme = t));
  }

  return { positionals, theme, mobile, full, steps };
}

function applyValueFlag(
  flag: string,
  value: string,
  steps: Step[],
  setTheme: (theme: ThemeArg) => void,
): void {
  switch (flag) {
    case '--theme':
      if (value !== 'light' && value !== 'dark' && value !== 'both') {
        throw new Error(`--theme must be light|dark|both, got "${value}"`);
      }
      setTheme(value);
      return;
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
    default:
      throw new Error(`Unknown flag: ${flag}`);
  }
}

/** Разворачивает `--theme both` в конкретные прогоны. */
export function themesFor(theme: ThemeArg): readonly ('light' | 'dark')[] {
  return theme === 'both' ? ['light', 'dark'] : [theme];
}
