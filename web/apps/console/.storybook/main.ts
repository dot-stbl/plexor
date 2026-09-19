import path from 'node:path';
import type { StorybookConfig } from '@storybook/react-vite';

/**
 * Storybook 9 config for the Plexor console.
 *
 * The react-vite builder auto-loads the app's `vite.config.ts` (react +
 * tailwindcss plugins), so Tailwind 4 processes `src/index.css` unchanged.
 * The `@` alias is re-asserted in `viteFinal` so stories keep resolving
 * `@/...` imports even if the auto-load behavior changes upstream.
 */
const config: StorybookConfig = {
  stories: ['../src/**/*.stories.tsx'],
  addons: ['@storybook/addon-themes'],
  framework: {
    name: '@storybook/react-vite',
    options: {},
  },
  viteFinal: async (config) => {
    config.resolve ??= {};
    config.resolve.alias = {
      ...config.resolve.alias,
      '@': path.resolve(__dirname, '../src'),
    };
    return config;
  },
};

export default config;
