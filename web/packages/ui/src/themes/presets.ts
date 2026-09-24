/**
 * Theme preset registry — values, in a form the browser can read directly.
 *
 * Each preset is a complete, single-rendering set of CSS custom properties.
 * A preset says nothing about whether it's "light" or "dark" beyond the
 * `isDarkPreferred` flag (which drives Tailwind's `.dark` class). The same
 * vocabulary is used by every preset — `--background`, `--foreground`,
 * `--primary`, `--muted`, and so on — and a preset's contribution is its
 * *values*, never its *keys*. `presets.test.ts` holds that promise.
 *
 * The three presets that ship in v1:
 *
 *   - `plexor-default-light` — the existing :root values from index.css.
 *     Kept verbatim so first paint and runtime match exactly.
 *   - `plexor-default-dark`  — the existing `.dark` values from index.css.
 *     Same.
 *   - `plexor-noir`          — a dark-only preset with a wider contrast
 *     range than `plexor-default-dark`, for the operator who lives in the
 *     dashboard all day. Background drops to oklch(8%) and foreground
 *     lifts to oklch(98%); surfaces and rules hold their relative spacing
 *     but are pushed further apart. The accent is bone-white (no hue), so
 *     the chrome stays colourless and the status semantics carry every
 *     signal. Always rendered dark regardless of the picker mode.
 *
 * To change a colour: edit it here, then mirror it into the matching
 * `:root` / `.dark` block in `index.css` (the first-paint baseline) and
 * rerun `bun run test`.
 */

/** The full token vocabulary. Every preset states every key. */
export type TokenName =
  // Surface
  | 'background'
  | 'card'
  | 'popover'
  | 'secondary'
  | 'muted'
  // Ink
  | 'foreground'
  | 'card-foreground'
  | 'popover-foreground'
  | 'secondary-foreground'
  | 'muted-foreground'
  // Border
  | 'border'
  | 'border-2'
  | 'input'
  // Plexor DS extras
  | 'surface-2'
  | 'surface-3'
  | 'fg-2'
  | 'muted-2'
  // Accent + ring
  | 'accent'
  | 'accent-foreground'
  | 'ring'
  // Destructive
  | 'destructive'
  | 'destructive-foreground'
  // Status semantics
  | 'ok'
  | 'ok-soft'
  | 'ok-ink'
  | 'err'
  | 'err-soft'
  | 'err-ink'
  | 'warn'
  | 'warn-soft'
  | 'warn-ink'
  | 'idle'
  | 'idle-soft'
  | 'idle-ink'
  | 'info'
  // Charts (shadcn slot — used by recharts etc.)
  | 'chart-1'
  | 'chart-2'
  | 'chart-3'
  | 'chart-4'
  | 'chart-5'
  // Sidebar (shadcn slot)
  | 'sidebar'
  | 'sidebar-foreground'
  | 'sidebar-primary'
  | 'sidebar-primary-foreground'
  | 'sidebar-accent'
  | 'sidebar-accent-foreground'
  | 'sidebar-border'
  | 'sidebar-ring'
  // Geometry — kept in the registry so a future theme can change radii.
  | 'radius';

export interface ThemePreset {
  /** Stable id. Written to `data-theme` on the root element. */
  readonly id: string;
  /** What a person sees in the picker. */
  readonly name: string;
  /** One line: what this theme is for. */
  readonly note: string;
  /** Whether the picker should drive the `.dark` Tailwind class on for this preset. */
  readonly isDarkPreferred: boolean;
  /** The complete token vocabulary, in CSS-ready values. */
  readonly tokens: Readonly<Record<TokenName, string>>;
}

/* ------------------------------------------------------------------ *
 * plexor-default-light
 *
 * The existing `:root` block from index.css, lifted verbatim so first
 * paint and runtime match. Every alias (`var(--foreground)`,
 * `var(--border)`, `var(--accent)`) is kept — `--card-foreground`
 * re-tracks `--foreground` whenever the latter changes.
 * ------------------------------------------------------------------ */
const PLEXOR_DEFAULT_LIGHT: ThemePreset = {
  id: 'plexor-default-light',
  name: 'Plexor — Default Light',
  note: 'The daylit board. Monochrome ink, hue-free accent, soft hairline borders. Same values that ship today.',
  isDarkPreferred: false,
  tokens: {
    /* ─── Surface ─── */
    background: 'oklch(98% 0.005 250)',
    card: 'oklch(100% 0 0)',
    popover: 'oklch(100% 0 0)',
    secondary: 'oklch(97% 0.006 250)',
    muted: 'oklch(97% 0.006 250)',

    /* ─── Ink ─── */
    foreground: 'oklch(22% 0.02 240)',
    'card-foreground': 'var(--foreground)',
    'popover-foreground': 'var(--foreground)',
    'secondary-foreground': 'var(--foreground)',
    'muted-foreground': 'oklch(50% 0.018 240)',

    /* ─── Borders ─── */
    border: 'oklch(91% 0.008 240)',
    'border-2': 'oklch(85% 0.008 240)',
    input: 'var(--border)',

    /* ─── Plexor DS extras ─── */
    'surface-2': 'oklch(97% 0.006 250)',
    'surface-3': 'oklch(95% 0.008 240)',
    'fg-2': 'oklch(35% 0.018 240)',
    'muted-2': 'oklch(55% 0.014 240)',

    /* ─── Accent (monochrome ink, NOT a hue) ─── */
    accent: 'oklch(28% 0.02 255)',
    'accent-foreground': 'oklch(100% 0 0)',
    ring: 'var(--accent)',

    /* ─── Destructive ─── */
    destructive: 'oklch(58% 0.20 25)',
    'destructive-foreground': 'oklch(98% 0.005 250)',

    /* ─── Status semantics ─── */
    ok: 'oklch(58% 0.16 145)',
    'ok-soft': 'oklch(95% 0.03 145)',
    'ok-ink': 'oklch(45% 0.13 145)',
    err: 'oklch(58% 0.20 25)',
    'err-soft': 'oklch(96% 0.03 25)',
    'err-ink': 'oklch(50% 0.17 25)',
    warn: 'oklch(58% 0.10 75)',
    'warn-soft': 'oklch(96% 0.05 75)',
    'warn-ink': 'oklch(48% 0.10 75)',
    idle: 'oklch(50% 0.012 240)',
    'idle-soft': 'oklch(95% 0.006 240)',
    'idle-ink': 'oklch(45% 0.012 240)',
    info: 'oklch(58% 0.14 240)',

    /* ─── Charts (shadcn defaults) ─── */
    'chart-1': 'oklch(0.87 0 0)',
    'chart-2': 'oklch(0.556 0 0)',
    'chart-3': 'oklch(0.439 0 0)',
    'chart-4': 'oklch(0.371 0 0)',
    'chart-5': 'oklch(0.269 0 0)',

    /* ─── Sidebar (shadcn defaults) ─── */
    sidebar: 'oklch(0.985 0 0)',
    'sidebar-foreground': 'oklch(0.145 0 0)',
    'sidebar-primary': 'oklch(0.205 0 0)',
    'sidebar-primary-foreground': 'oklch(0.985 0 0)',
    'sidebar-accent': 'oklch(0.97 0 0)',
    'sidebar-accent-foreground': 'oklch(0.205 0 0)',
    'sidebar-border': 'oklch(0.922 0 0)',
    'sidebar-ring': 'oklch(0.708 0 0)',

    /* ─── Geometry ─── */
    radius: '0.5rem',
  },
};

/* ------------------------------------------------------------------ *
 * plexor-default-dark
 *
 * The existing `.dark` block from index.css, lifted verbatim. Same
 * invariant as the light preset: first paint, runtime, and the test
 * baseline all carry the same numbers.
 * ------------------------------------------------------------------ */
const PLEXOR_DEFAULT_DARK: ThemePreset = {
  id: 'plexor-default-dark',
  name: 'Plexor — Default Dark',
  note: 'The night board. Same monochrome language as light, inverted: ink foreground on near-black surfaces, bone-white accent.',
  isDarkPreferred: true,
  tokens: {
    /* ─── Surface ─── */
    background: 'oklch(17% 0.012 250)',
    card: 'oklch(21% 0.014 250)',
    popover: 'oklch(21% 0.014 250)',
    secondary: 'oklch(25% 0.015 250)',
    muted: 'oklch(25% 0.015 250)',

    /* ─── Ink ─── */
    foreground: 'oklch(94% 0.006 250)',
    'card-foreground': 'var(--foreground)',
    'popover-foreground': 'var(--foreground)',
    'secondary-foreground': 'var(--foreground)',
    'muted-foreground': 'oklch(66% 0.012 250)',

    /* ─── Borders ─── */
    border: 'oklch(30% 0.012 250)',
    'border-2': 'oklch(38% 0.014 250)',
    input: 'var(--border)',

    /* ─── Plexor DS extras ─── */
    'surface-2': 'oklch(25% 0.015 250)',
    'surface-3': 'oklch(28% 0.016 250)',
    'fg-2': 'oklch(82% 0.008 250)',
    'muted-2': 'oklch(62% 0.012 250)',

    /* ─── Accent (bone, NOT a hue) ─── */
    accent: 'oklch(92% 0.006 250)',
    'accent-foreground': 'oklch(20% 0.02 250)',
    ring: 'var(--accent)',

    /* ─── Destructive ─── */
    destructive: 'oklch(68% 0.18 25)',
    'destructive-foreground': 'oklch(98% 0.005 250)',

    /* ─── Status semantics ─── */
    ok: 'oklch(72% 0.15 145)',
    'ok-soft': 'oklch(32% 0.05 145)',
    'ok-ink': 'oklch(85% 0.13 145)',
    err: 'oklch(68% 0.18 25)',
    'err-soft': 'oklch(33% 0.06 25)',
    'err-ink': 'oklch(83% 0.14 25)',
    warn: 'oklch(75% 0.10 75)',
    'warn-soft': 'oklch(33% 0.05 75)',
    'warn-ink': 'oklch(87% 0.10 75)',
    idle: 'oklch(72% 0.012 240)',
    'idle-soft': 'oklch(30% 0.006 250)',
    'idle-ink': 'oklch(72% 0.01 250)',
    info: 'oklch(72% 0.13 240)',

    /* ─── Charts (shadcn gray scale — same in both modes by design) ─── */
    'chart-1': 'oklch(0.87 0 0)',
    'chart-2': 'oklch(0.556 0 0)',
    'chart-3': 'oklch(0.439 0 0)',
    'chart-4': 'oklch(0.371 0 0)',
    'chart-5': 'oklch(0.269 0 0)',

    /* ─── Sidebar (shadcn defaults) ─── */
    sidebar: 'oklch(0.205 0 0)',
    'sidebar-foreground': 'oklch(0.985 0 0)',
    'sidebar-primary': 'oklch(0.488 0.243 264.376)',
    'sidebar-primary-foreground': 'oklch(0.985 0 0)',
    'sidebar-accent': 'oklch(0.269 0 0)',
    'sidebar-accent-foreground': 'oklch(0.985 0 0)',
    'sidebar-border': 'oklch(1 0 0 / 10%)',
    'sidebar-ring': 'oklch(0.556 0 0)',

    /* ─── Geometry ─── */
    radius: '0.5rem',
  },
};

/* ------------------------------------------------------------------ *
 * plexor-noir
 *
 * Built for the operator who lives in the dashboard: deeper blacks,
 * brighter foregrounds, wider gaps between every step on the surface
 * ramp. The chrome is fully colourless — accent is bone, ring is bone,
 * idle is bone — so the status semantics carry every signal on their
 * own.
 *
 * Always rendered dark (`isDarkPreferred = true`). The picker mode is
 * ignored; the only way to leave this preset is to pick another one.
 * ------------------------------------------------------------------ */
const PLEXOR_NOIR: ThemePreset = {
  id: 'plexor-noir',
  name: 'Plexor — Noir',
  note: 'High-contrast monochrome dark. For the operator who lives in the dashboard — background at oklch(8%), foreground at oklch(98%), accent bone-white so every status has the lane to itself.',
  isDarkPreferred: true,
  tokens: {
    /* ─── Surface — pushed lower than default-dark ─── */
    background: 'oklch(8% 0 0)',
    card: 'oklch(11% 0 0)',
    popover: 'oklch(11% 0 0)',
    secondary: 'oklch(14% 0 0)',
    muted: 'oklch(14% 0 0)',

    /* ─── Ink — pushed higher than default-dark ─── */
    foreground: 'oklch(98% 0 0)',
    'card-foreground': 'var(--foreground)',
    'popover-foreground': 'var(--foreground)',
    'secondary-foreground': 'var(--foreground)',
    'muted-foreground': 'oklch(78% 0 0)',

    /* ─── Borders — wider gap than default-dark ─── */
    border: 'oklch(22% 0 0)',
    'border-2': 'oklch(35% 0 0)',
    input: 'var(--border)',

    /* ─── Plexor DS extras ─── */
    'surface-2': 'oklch(14% 0 0)',
    'surface-3': 'oklch(18% 0 0)',
    'fg-2': 'oklch(92% 0 0)',
    'muted-2': 'oklch(60% 0 0)',

    /* ─── Accent — bone, NOT a hue ─── */
    accent: 'oklch(98% 0 0)',
    'accent-foreground': 'oklch(8% 0 0)',
    ring: 'var(--accent)',

    /* ─── Destructive — hotter than default-dark ─── */
    destructive: 'oklch(72% 0.22 25)',
    'destructive-foreground': 'oklch(8% 0 0)',

    /* ─── Status semantics — brighter, wider gaps ─── */
    ok: 'oklch(80% 0.20 145)',
    'ok-soft': 'oklch(28% 0.07 145)',
    'ok-ink': 'oklch(90% 0.16 145)',
    err: 'oklch(72% 0.22 25)',
    'err-soft': 'oklch(30% 0.08 25)',
    'err-ink': 'oklch(88% 0.18 25)',
    warn: 'oklch(82% 0.14 75)',
    'warn-soft': 'oklch(30% 0.06 75)',
    'warn-ink': 'oklch(90% 0.12 75)',
    idle: 'oklch(78% 0 0)',
    'idle-soft': 'oklch(22% 0 0)',
    'idle-ink': 'oklch(80% 0 0)',
    info: 'oklch(80% 0.15 240)',

    /* ─── Charts (shadcn gray scale — same in both modes by design) ─── */
    'chart-1': 'oklch(0.87 0 0)',
    'chart-2': 'oklch(0.556 0 0)',
    'chart-3': 'oklch(0.439 0 0)',
    'chart-4': 'oklch(0.371 0 0)',
    'chart-5': 'oklch(0.269 0 0)',

    /* ─── Sidebar (shadcn dark defaults — no chart palette mismatch) ─── */
    sidebar: 'oklch(0.205 0 0)',
    'sidebar-foreground': 'oklch(0.985 0 0)',
    'sidebar-primary': 'oklch(0.488 0.243 264.376)',
    'sidebar-primary-foreground': 'oklch(0.985 0 0)',
    'sidebar-accent': 'oklch(0.269 0 0)',
    'sidebar-accent-foreground': 'oklch(0.985 0 0)',
    'sidebar-border': 'oklch(1 0 0 / 10%)',
    'sidebar-ring': 'oklch(0.556 0 0)',

    /* ─── Geometry ─── */
    radius: '0.5rem',
  },
};

/** Every preset, in the order the picker will eventually list them. */
export const presets: readonly ThemePreset[] = [
  PLEXOR_DEFAULT_LIGHT,
  PLEXOR_DEFAULT_DARK,
  PLEXOR_NOIR,
];

/** The preset a fresh user lands on. Used as the boot-config default. */
export const DEFAULT_PRESET_ID: string = PLEXOR_DEFAULT_LIGHT.id;