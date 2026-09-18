/**
 * Community themes — the marketplace layer on top of the built-in preset
 * registry. v1 ships two synthesised starter themes; the registry mechanism
 * (separate from `presets.ts` so built-in tokens never drift) gives Phase
 * 5+ a hook for fetching signed manifests from an external registry.
 *
 * Wire model: a `CommunityTheme` is a `ThemePreset` plus three authored
 * fields (`author`, `version`, `marketplace: true`). The `marketplace: true`
 * literal gives the registry a structural way to discriminate them from
 * built-in presets without an `isCommunity` runtime check.
 *
 * Contrast notes (v1 — formal contrast floors land in Phase 5+):
 *   - `synthwave-night` ships two presets (`-light` + `-dark`). Both use a
 *     magenta-primary / cyan-accent palette against ink; foreground/background
 *     lightnesses aim for ≥ 4.5:1 contrast but are not asserted by tests.
 *   - `paper-light` is light-only; ink-on-warm-cream with neutral accents.
 *
 * To extend later with a real publisher feed, populate this list from a
 * signed manifest fetch + validate against a Zod schema — the export shape
 * stays identical so the marketplace UI / hooks don't change.
 */
import type { ThemePreset } from './presets';

export interface CommunityTheme extends ThemePreset {
  readonly author: string;
  readonly version: string;
  /** Discriminator literal — always `true` for community themes. */
  readonly marketplace: true;
}

/* ------------------------------------------------------------------ *
 * Synthwave Night — dark, neon-on-navy
 *
 * `synthwave-night-dark` ships the canonical synthwave look: deep navy
 * background, magenta primary, cyan accents. Saturation is dialled back
 * from full neon so foreground ink stays readable (~oklch 94% on
 * oklch 15%). Status semantics keep the wider gap so ok/err/warn lanes
 * don't blend into the neon palette.
 * ------------------------------------------------------------------ */
const SYNTHWAVE_NIGHT_DARK: CommunityTheme = {
  id: 'synthwave-night-dark',
  name: 'Synthwave Night — Dark',
  note: 'Neon-on-navy. Magenta primary, cyan accents, status lanes intact. Always rendered dark — the neon palette has no light counterpart.',
  author: 'plexor-themes',
  version: '0.1.0',
  marketplace: true,
  isDarkPreferred: true,
  tokens: {
    /* ─── Surface — deep navy with a touch of violet ─── */
    background: 'oklch(15% 0.04 270)',
    card: 'oklch(19% 0.045 270)',
    popover: 'oklch(19% 0.045 270)',
    secondary: 'oklch(23% 0.05 270)',
    muted: 'oklch(23% 0.05 270)',

    /* ─── Ink — slightly cool off-white ─── */
    foreground: 'oklch(94% 0.02 270)',
    'card-foreground': 'var(--foreground)',
    'popover-foreground': 'var(--foreground)',
    'secondary-foreground': 'var(--foreground)',
    'muted-foreground': 'oklch(70% 0.03 270)',

    /* ─── Borders — violet-tinted hairline ─── */
    border: 'oklch(32% 0.05 280)',
    'border-2': 'oklch(42% 0.06 285)',
    input: 'var(--border)',

    /* ─── Plexor DS extras ─── */
    'surface-2': 'oklch(23% 0.05 270)',
    'surface-3': 'oklch(27% 0.05 275)',
    'fg-2': 'oklch(82% 0.04 270)',
    'muted-2': 'oklch(56% 0.04 275)',

    /* ─── Accent — neon magenta ─── */
    accent: 'oklch(72% 0.22 340)',
    'accent-foreground': 'oklch(12% 0.05 340)',
    ring: 'var(--accent)',

    /* ─── Destructive — hot magenta-red, won't be confused with accent ─── */
    destructive: 'oklch(68% 0.22 25)',
    'destructive-foreground': 'oklch(98% 0.005 250)',

    /* ─── Status semantics — cyan-tinted ok, magenta err, gold warn ─── */
    ok: 'oklch(80% 0.18 200)',
    'ok-soft': 'oklch(30% 0.06 200)',
    'ok-ink': 'oklch(90% 0.16 200)',
    err: 'oklch(70% 0.22 25)',
    'err-soft': 'oklch(30% 0.08 25)',
    'err-ink': 'oklch(88% 0.18 25)',
    warn: 'oklch(82% 0.16 75)',
    'warn-soft': 'oklch(32% 0.06 75)',
    'warn-ink': 'oklch(90% 0.14 75)',
    idle: 'oklch(72% 0.04 270)',
    'idle-soft': 'oklch(26% 0.04 275)',
    'idle-ink': 'oklch(76% 0.04 270)',
    info: 'oklch(78% 0.18 240)',

    /* ─── Charts (cyan → magenta gradient, picks up the synthwave palette) ─── */
    'chart-1': 'oklch(72% 0.22 340)',
    'chart-2': 'oklch(78% 0.18 200)',
    'chart-3': 'oklch(70% 0.20 290)',
    'chart-4': 'oklch(82% 0.16 75)',
    'chart-5': 'oklch(66% 0.04 270)',

    /* ─── Sidebar (carries the same accents as the surface ramp) ─── */
    sidebar: 'oklch(0.18 0.04 270)',
    'sidebar-foreground': 'oklch(0.94 0.02 270)',
    'sidebar-primary': 'oklch(0.72 0.22 340)',
    'sidebar-primary-foreground': 'oklch(0.12 0.05 340)',
    'sidebar-accent': 'oklch(0.26 0.05 270)',
    'sidebar-accent-foreground': 'oklch(0.94 0.02 270)',
    'sidebar-border': 'oklch(1 0 0 / 10%)',
    'sidebar-ring': 'oklch(0.72 0.22 340)',

    /* ─── Geometry — slightly tighter radii, neon-precise ─── */
    radius: '0.375rem',
  },
};

/* ------------------------------------------------------------------ *
 * Synthwave Night — Light (the daylight variant)
 *
 * Same palette discipline as `-dark` (magenta / cyan), but on a soft
 * warm-cream background and slightly desaturated so the page reads as a
 * printed poster, not a glow-stick. Status lanes hold their colour
 * identity even at lower chroma — err stays red, ok stays cyan, warn
 * stays gold.
 * ------------------------------------------------------------------ */
const SYNTHWAVE_NIGHT_LIGHT: CommunityTheme = {
  id: 'synthwave-night-light',
  name: 'Synthwave Night — Light',
  note: 'Daylight variant of the synthwave palette — magenta + cyan on a soft warm page, sat dialled back so ink stays readable. Always rendered light.',
  author: 'plexor-themes',
  version: '0.1.0',
  marketplace: true,
  isDarkPreferred: false,
  tokens: {
    /* ─── Surface — warm cream, hint of pink ─── */
    background: 'oklch(96% 0.02 50)',
    card: 'oklch(99% 0.015 50)',
    popover: 'oklch(99% 0.015 50)',
    secondary: 'oklch(94% 0.025 60)',
    muted: 'oklch(94% 0.025 60)',

    /* ─── Ink — dark plum, picks up the magenta hue at low chroma ─── */
    foreground: 'oklch(22% 0.05 320)',
    'card-foreground': 'var(--foreground)',
    'popover-foreground': 'var(--foreground)',
    'secondary-foreground': 'var(--foreground)',
    'muted-foreground': 'oklch(48% 0.04 320)',

    /* ─── Borders — pink hairline ─── */
    border: 'oklch(88% 0.03 320)',
    'border-2': 'oklch(82% 0.04 325)',
    input: 'var(--border)',

    /* ─── Plexor DS extras ─── */
    'surface-2': 'oklch(94% 0.025 60)',
    'surface-3': 'oklch(91% 0.03 65)',
    'fg-2': 'oklch(34% 0.05 320)',
    'muted-2': 'oklch(60% 0.04 320)',

    /* ─── Accent — magenta, slightly desaturated vs the dark variant ─── */
    accent: 'oklch(58% 0.22 340)',
    'accent-foreground': 'oklch(99% 0.01 50)',
    ring: 'var(--accent)',

    /* ─── Destructive ─── */
    destructive: 'oklch(56% 0.22 25)',
    'destructive-foreground': 'oklch(98% 0.005 250)',

    /* ─── Status semantics ─── */
    ok: 'oklch(54% 0.16 200)',
    'ok-soft': 'oklch(94% 0.04 200)',
    'ok-ink': 'oklch(42% 0.14 200)',
    err: 'oklch(56% 0.22 25)',
    'err-soft': 'oklch(94% 0.04 25)',
    'err-ink': 'oklch(46% 0.18 25)',
    warn: 'oklch(58% 0.14 75)',
    'warn-soft': 'oklch(94% 0.05 75)',
    'warn-ink': 'oklch(46% 0.12 75)',
    idle: 'oklch(48% 0.03 320)',
    'idle-soft': 'oklch(93% 0.015 320)',
    'idle-ink': 'oklch(42% 0.03 320)',
    info: 'oklch(54% 0.16 240)',

    /* ─── Charts (same gradient, slightly higher lightness) ─── */
    'chart-1': 'oklch(58% 0.22 340)',
    'chart-2': 'oklch(54% 0.16 200)',
    'chart-3': 'oklch(52% 0.18 290)',
    'chart-4': 'oklch(58% 0.14 75)',
    'chart-5': 'oklch(54% 0.03 320)',

    /* ─── Sidebar ─── */
    sidebar: 'oklch(0.985 0.01 50)',
    'sidebar-foreground': 'oklch(0.22 0.05 320)',
    'sidebar-primary': 'oklch(0.58 0.22 340)',
    'sidebar-primary-foreground': 'oklch(0.99 0.01 50)',
    'sidebar-accent': 'oklch(0.94 0.025 60)',
    'sidebar-accent-foreground': 'oklch(0.22 0.05 320)',
    'sidebar-border': 'oklch(0.88 0.03 320)',
    'sidebar-ring': 'oklch(0.58 0.22 340)',

    /* ─── Geometry — tighter radii, match the dark variant ─── */
    radius: '0.375rem',
  },
};

/* ------------------------------------------------------------------ *
 * Paper Light — high-contrast monochrome, warm
 *
 * Built for long reading sessions: warm-cream background, near-black
 * foreground, paper-stock neutrals. Accent is a warm graphite (no hue)
 * so the page reads like a printed document; the only coloured tokens
 * are status semantics, which stay narrow to avoid flagging noise.
 *
 * Light only — `isDarkPreferred: false`. A dark counterpart isn't planned
 * because the whole point is "looks like paper under daylight".
 * ------------------------------------------------------------------ */
const PAPER_LIGHT: CommunityTheme = {
  id: 'paper-light',
  name: 'Paper Light',
  note: 'High-contrast monochrome on warm paper-stock. For long reading sessions — accent is graphite, status lanes are the only colour in the chrome.',
  author: 'plexor-themes',
  version: '0.1.0',
  marketplace: true,
  isDarkPreferred: false,
  tokens: {
    /* ─── Surface — warm cream, paper stock ─── */
    background: 'oklch(96% 0.012 80)',
    card: 'oklch(98% 0.008 80)',
    popover: 'oklch(98% 0.008 80)',
    secondary: 'oklch(94% 0.014 80)',
    muted: 'oklch(94% 0.014 80)',

    /* ─── Ink — near-black, warm ─── */
    foreground: 'oklch(18% 0.005 80)',
    'card-foreground': 'var(--foreground)',
    'popover-foreground': 'var(--foreground)',
    'secondary-foreground': 'var(--foreground)',
    'muted-foreground': 'oklch(46% 0.012 80)',

    /* ─── Borders — hairline, low chroma ─── */
    border: 'oklch(86% 0.01 80)',
    'border-2': 'oklch(78% 0.012 80)',
    input: 'var(--border)',

    /* ─── Plexor DS extras ─── */
    'surface-2': 'oklch(94% 0.014 80)',
    'surface-3': 'oklch(91% 0.016 80)',
    'fg-2': 'oklch(30% 0.008 80)',
    'muted-2': 'oklch(58% 0.01 80)',

    /* ─── Accent — graphite (no hue, like ink in a fountain pen) ─── */
    accent: 'oklch(22% 0.005 80)',
    'accent-foreground': 'oklch(98% 0.008 80)',
    ring: 'var(--accent)',

    /* ─── Destructive ─── */
    destructive: 'oklch(50% 0.18 25)',
    'destructive-foreground': 'oklch(98% 0.008 80)',

    /* ─── Status semantics — narrow chroma so they read as ink stamps ─── */
    ok: 'oklch(48% 0.14 145)',
    'ok-soft': 'oklch(94% 0.025 145)',
    'ok-ink': 'oklch(36% 0.12 145)',
    err: 'oklch(50% 0.18 25)',
    'err-soft': 'oklch(94% 0.03 25)',
    'err-ink': 'oklch(40% 0.16 25)',
    warn: 'oklch(52% 0.10 75)',
    'warn-soft': 'oklch(94% 0.04 75)',
    'warn-ink': 'oklch(42% 0.10 75)',
    idle: 'oklch(46% 0.008 80)',
    'idle-soft': 'oklch(93% 0.008 80)',
    'idle-ink': 'oklch(40% 0.008 80)',
    info: 'oklch(48% 0.12 240)',

    /* ─── Charts (cool gray scale so charts stay separable from ink) ─── */
    'chart-1': 'oklch(0.3 0 0)',
    'chart-2': 'oklch(0.45 0 0)',
    'chart-3': 'oklch(0.58 0 0)',
    'chart-4': 'oklch(0.7 0 0)',
    'chart-5': 'oklch(0.82 0 0)',

    /* ─── Sidebar ─── */
    sidebar: 'oklch(0.98 0.008 80)',
    'sidebar-foreground': 'oklch(0.18 0.005 80)',
    'sidebar-primary': 'oklch(0.22 0.005 80)',
    'sidebar-primary-foreground': 'oklch(0.98 0.008 80)',
    'sidebar-accent': 'oklch(0.94 0.014 80)',
    'sidebar-accent-foreground': 'oklch(0.18 0.005 80)',
    'sidebar-border': 'oklch(0.86 0.01 80)',
    'sidebar-ring': 'oklch(0.22 0.005 80)',

    /* ─── Geometry — tighter radii, more "book" than "card" ─── */
    radius: '0.25rem',
  },
};

/** Every community theme, in the order the marketplace UI will list them. */
export const communityThemes: readonly CommunityTheme[] = [
  SYNTHWAVE_NIGHT_DARK,
  SYNTHWAVE_NIGHT_LIGHT,
  PAPER_LIGHT,
];

/**
 * Look up a community theme by id. Returns `null` on miss (callers in the
 * marketplace UI treat that as "not installed" rather than a crash).
 */
export function getCommunityTheme(id: string): CommunityTheme | null {
  return communityThemes.find((theme) => theme.id === id) ?? null;
}
