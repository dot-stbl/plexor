/**
 * MDX components — single barrel so `mdx-components.tsx` can register
 * them in one import. Re-exports each component alongside the props
 * type so MDX frontmatter can reference them when needed.
 */
export { Callout } from './callout';
export type { CalloutProps, CalloutType } from './callout';

export { Step } from './step';
export type { StepProps } from './step';

export { Screenshot, ScreenshotPlaceholder } from './screenshot';
export type {
  ScreenshotProps,
  ScreenshotPlaceholderProps,
} from './screenshot';

export { Kbd } from './kbd';
export type { KbdProps } from './kbd';

export { Diagram, DiagramPlaceholder } from './diagram';
export type { DiagramProps, DiagramPlaceholderProps } from './diagram';

export { PlatformMatrix } from './platform-matrix';
export type {
  PlatformMatrixProps,
  PlatformMatrixRow,
} from './platform-matrix';
