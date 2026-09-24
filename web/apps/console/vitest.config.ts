/// <reference types="vitest" />
import { defineConfig, mergeConfig } from 'vitest/config';
import viteConfig from './vite.config';

export default mergeConfig(
  viteConfig,
  defineConfig({
    test: {
      environment: 'jsdom',
      globals: true,
      setupFiles: ['./src/test-setup.ts'],
      // Exclude the Playwright diagnostic scripts in repro/ — those are
      // run via `bunx playwright test`, not vitest. They use
      // @playwright/test which vitest can't parse.
      exclude: ['**/node_modules/**', 'repro/**'],
    },
  }),
);