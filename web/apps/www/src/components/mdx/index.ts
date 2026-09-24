/**
 * MDX components — single barrel so `mdx-components.tsx` can register
 * them in one import. Re-exports each component alongside the props
 * type so MDX frontmatter can reference them when needed.
 */
export { Choice, Choices } from './choices';
export type { ChoiceProps, ChoicesProps } from './choices';

export { Accordion, Accordions } from './accordion';
export type { AccordionProps, AccordionsProps } from './accordion';

export { Callout } from './callout';
export type { CalloutProps, CalloutType } from './callout';

export { Diagram, DiagramPlaceholder } from './diagram';
export type { DiagramProps, DiagramPlaceholderProps } from './diagram';

export { Kbd } from './kbd';
export type { KbdProps } from './kbd';

export { PlatformMatrix } from './platform-matrix';
export type {
  PlatformMatrixProps,
  PlatformMatrixRow,
} from './platform-matrix';

export { Screenshot, ScreenshotPlaceholder } from './screenshot';
export type {
  ScreenshotProps,
  ScreenshotPlaceholderProps,
} from './screenshot';

export { Step } from './step';
export type { StepProps } from './step';

export { TypeRow, TypeTable } from './type-table';
export type { TypeRowProps, TypeTableProps } from './type-table';
