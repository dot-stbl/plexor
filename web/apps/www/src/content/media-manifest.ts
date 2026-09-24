/**
 * The landing page's screenshots — hand-written, not generated. At most
 * 2-3 stills on the whole landing (product owner feedback: "just
 * screenshots, and rarely"), each a real console UI capture (mock data)
 * from `scripts/capture/**`, theme-aware (`light`/`dark`).
 *
 * Re-run `bun run capture` (from `web/apps/www`) to refresh the files
 * under `public/media/*.webp` after a console UI change; this list only
 * needs an edit if the SET of screenshots used changes, not on every
 * capture run.
 */

export interface Screenshot {
  readonly id: string;
  /** Accessible description of what the screenshot shows. */
  readonly alt: string;
  readonly light: string;
  readonly dark: string;
  readonly width: number;
  readonly height: number;
}

const SHOT_WIDTH = 1280;
const SHOT_HEIGHT = 800;

export const SCREENSHOTS: readonly Screenshot[] = [
  {
    id: 'hero',
    alt: 'The Plexor console virtual machine list — eight VMs with status, IP, flavor and disk.',
    light: '/media/hero.light.webp',
    dark: '/media/hero.dark.webp',
    width: SHOT_WIDTH,
    height: SHOT_HEIGHT,
  },
  {
    id: 'catalog',
    alt: 'The Plexor console managed PostgreSQL catalog page, ready to create a cluster.',
    light: '/media/catalog.light.webp',
    dark: '/media/catalog.dark.webp',
    width: SHOT_WIDTH,
    height: SHOT_HEIGHT,
  },
  {
    id: 'audit',
    alt: 'The Plexor console audit log — a filterable, tenant-scoped timeline of every action.',
    light: '/media/audit.light.webp',
    dark: '/media/audit.dark.webp',
    width: SHOT_WIDTH,
    height: SHOT_HEIGHT,
  },
];

export function getScreenshot(id: string): Screenshot | undefined {
  return SCREENSHOTS.find((s) => s.id === id);
}

export function requireScreenshot(id: string): Screenshot {
  const shot = getScreenshot(id);
  if (!shot) {
    throw new Error(`Screenshot "${id}" has no entry in src/content/media-manifest.ts.`);
  }
  return shot;
}
