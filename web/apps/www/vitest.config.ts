import { defineConfig } from 'vitest/config';
import path from 'node:path';

/**
 * Standalone vitest config (not merged with `vite.config.ts` — this app
 * has no MSW/mock layer that would make merging the full app config
 * worthwhile, and keeping the test environment minimal avoids pulling
 * the TanStack Router codegen plugin or the MDX pipeline into every
 * `vitest run`). The `@` alias is duplicated here to match
 * `vite.config.ts` / `tsconfig.json`'s `paths` — needed as soon as any
 * test imports app source via `@/...` (e.g. `src/content/search-index.ts`
 * importing `@/components/docs/docs-sidebar`).
 */
export default defineConfig({
  resolve: {
    alias: {
      '@': path.resolve(__dirname, './src'),
    },
  },
  test: {
    environment: 'jsdom',
    globals: true,
  },
});
