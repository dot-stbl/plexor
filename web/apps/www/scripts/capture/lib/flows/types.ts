import type { Page } from 'playwright';

export interface Flow {
  /** Slug — also the output filename stem (`<id>.<theme>.webp`). */
  readonly id: string;
  /** Human title — used only for this script's own console logging. */
  readonly title: string;
  /** Route the flow starts on, relative to the console's base URL. */
  readonly startPath: string;
  /** Drives the page from first paint to the end state this screenshot shows. */
  run(page: Page): Promise<void>;
}
