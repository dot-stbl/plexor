'use client';

import { useState } from 'react';
import { Check } from '@nine-thirty-five/material-symbols-react/rounded/700';

/**
 * Plexor theme presets — three first-party variants from the console's
 * `web/packages/tokens` registry. The landing page renders all three in a
 * side-by-side preview with a click-to-activate control, so a visitor can
 * see Plexor's monochrome design language in action without registering.
 *
 * The tokens here are the actual CSS custom properties the console ships —
 * sourced from `web/apps/console/src/shared/lib/themes/presets.ts` so the
 * landing previews render exactly what operators see in the console.
 */
type Mode = 'light' | 'dark';

interface ThemePreset {
  readonly id: string;
  readonly name: string;
  readonly note: string;
  readonly mode: Mode;
  readonly tokens: Readonly<Record<string, string>>;
}

const PRESETS: readonly ThemePreset[] = [
  {
    id: 'paper-light',
    name: 'Paper',
    note: 'Daylit board. Monochrome ink, hue-free accent, soft hairline borders.',
    mode: 'light',
    tokens: {
      background: 'oklch(98% 0.005 250)',
      surface: 'oklch(100% 0 0)',
      surfaceAlt: 'oklch(97% 0.006 250)',
      foreground: 'oklch(22% 0.02 240)',
      mutedForeground: 'oklch(50% 0.018 240)',
      border: 'oklch(91% 0.008 240)',
      accent: 'oklch(28% 0.02 255)',
      accentFg: 'oklch(100% 0 0)',
    },
  },
  {
    id: 'paper-dark',
    name: 'Midnight',
    note: 'Night board. Inverted monochrome: ink foreground on near-black surfaces, bone-white accent.',
    mode: 'dark',
    tokens: {
      background: 'oklch(17% 0.012 250)',
      surface: 'oklch(21% 0.014 250)',
      surfaceAlt: 'oklch(25% 0.015 250)',
      foreground: 'oklch(94% 0.006 250)',
      mutedForeground: 'oklch(66% 0.012 250)',
      border: 'oklch(30% 0.012 250)',
      accent: 'oklch(92% 0.006 250)',
      accentFg: 'oklch(20% 0.02 250)',
    },
  },
  {
    id: 'noir',
    name: 'Noir',
    note: 'High-contrast monochrome dark. Background at oklch(8%), foreground at oklch(98%).',
    mode: 'dark',
    tokens: {
      background: 'oklch(8% 0 0)',
      surface: 'oklch(11% 0 0)',
      surfaceAlt: 'oklch(14% 0 0)',
      foreground: 'oklch(98% 0 0)',
      mutedForeground: 'oklch(78% 0 0)',
      border: 'oklch(22% 0 0)',
      accent: 'oklch(98% 0 0)',
      accentFg: 'oklch(8% 0 0)',
    },
  },
];

/**
 * Single tile — a labelled swatch of one preset. The tile renders the
 * preset's tokens as inline CSS custom properties so the preview is
 * accurate (no fake palette), and exposes a click handler that fires
 * `onActivate(id)` for parent state.
 */
function ThemeTile({
  preset,
  active,
  onActivate,
}: {
  preset: ThemePreset;
  active: boolean;
  onActivate: (id: string) => void;
}) {
  const t = preset.tokens;
  const styleVars: Record<string, string> = {};
  for (const [k, v] of Object.entries(t)) styleVars[`--preview-${k}`] = v;

  return (
    <button
      type="button"
      onClick={() => onActivate(preset.id)}
      className={
        'docs-theme-tile group relative w-full overflow-hidden rounded-xl border text-left ' +
        (active
          ? 'border-fd-ring ring-2 ring-fd-ring ring-offset-2 ring-offset-fd-background'
          : 'border-fd-border hover:border-fd-muted-foreground')
      }
      aria-pressed={active}
      aria-label={`Activate ${preset.name} theme`}
    >
      <div
        className="h-32 w-full"
        style={{
          ...styleVars,
          background: 'var(--preview-background)',
          borderBottom: '1px solid var(--preview-border)',
        }}
      >
        <div
          className="flex h-full w-full flex-col gap-2 p-4"
          style={{ color: 'var(--preview-foreground)' }}
        >
          <div
            className="flex h-6 w-1/3 rounded"
            style={{ background: 'var(--preview-accent)' }}
          />
          <div
            className="h-3 w-2/3 rounded"
            style={{ background: 'var(--preview-surfaceAlt)' }}
          />
          <div
            className="h-3 w-1/2 rounded"
            style={{ background: 'var(--preview-surfaceAlt)', opacity: 0.6 }}
          />
          <div className="mt-auto flex items-center gap-2">
            <div
              className="size-2 rounded-full"
              style={{ background: 'var(--preview-accent)' }}
            />
            <span
              className="text-xs"
              style={{ color: 'var(--preview-mutedForeground)' }}
            >
              {preset.mode === 'dark' ? 'Dark' : 'Light'}
            </span>
          </div>
        </div>
      </div>
      <div className="flex items-center justify-between px-4 py-3">
        <div>
          <div className="text-sm font-medium">{preset.name}</div>
          <div className="text-fd-muted-foreground text-xs">{preset.note}</div>
        </div>
        {active ? (
          <Check className="text-fd-primary size-4 shrink-0" aria-hidden />
        ) : null}
      </div>
    </button>
  );
}

/**
 * Three presets side-by-side. The active one is rendered with a ring; the
 * bottom CTA reveals "this is what your operators actually pick" — a
 * concrete example of Plexor's theme marketplace capability, not a stock
 * "light/dark toggle".
 */
export function ThemePreview() {
  const [activeId, setActiveId] = useState<string>('paper-light');
  const active = PRESETS.find((p) => p.id === activeId) ?? PRESETS[0];

  return (
    <div className="landing-block not-prose my-2">
      <div className="mb-4 flex items-baseline justify-between">
        <h3 className="m-0 text-base font-medium">
          Темы, которые операторы реально используют
        </h3>
        <span className="text-fd-muted-foreground text-xs">
          активная: <code className="font-mono">{active.id}</code>
        </span>
      </div>
      <div className="grid grid-cols-1 gap-3 sm:grid-cols-3">
        {PRESETS.map((preset) => (
          <ThemeTile
            key={preset.id}
            preset={preset}
            active={preset.id === activeId}
            onActivate={setActiveId}
          />
        ))}
      </div>
      <p className="text-fd-muted-foreground mt-3 text-xs">
        Каждая тема — это токены OKLCH, не скриншот. Свой пресет добавляется в{' '}
        <code className="font-mono">web/packages/tokens/src/themes/&lt;id&gt;.ts</code>{' '}
        и подхватывается реестром без правок в console.
      </p>
    </div>
  );
}