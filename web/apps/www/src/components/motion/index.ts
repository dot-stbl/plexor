/**
 * Motion foundation — barrel. `Reveal`/`Stagger` and their supporting
 * hooks, so `src/components/marketing/**` can import everything from
 * `@/components/motion` without reaching into individual files.
 *
 * The canvas topology field (`SceneBackground`), the scroll-scrubbed
 * video primitives (`ThemedVideo`, `ScrollScene`) and the CSS
 * `DotGrid`/`GridLines` textures were removed per the product owner's
 * request to drop decorative backgrounds and all video from the landing
 * — screenshots only now (see `src/content/media-manifest.ts`).
 */
export { Reveal } from './reveal';
export type { RevealProps } from './reveal';

export { Stagger, StaggerItem } from './stagger';
export type { StaggerProps, StaggerItemProps } from './stagger';

export { useReducedMotionSafe } from './use-reduced-motion-safe';
export { useThemeName } from './use-theme-name';
export type { ThemeName } from './use-theme-name';
