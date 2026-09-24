import type { Preview } from '@storybook/react-vite';
import { withThemeByClassName } from '@storybook/addon-themes';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';

// App tokens (OKLCH CSS variables + Tailwind 4 theme) and i18n bootstrap —
// mirrors the provider stack in src/main.tsx, minus router/toaster.
import '../src/index.css';
import i18n from '../src/shared/lib/i18n';

const queryClient = new QueryClient({
  defaultOptions: {
    queries: { retry: false },
  },
});

const preview: Preview = {
  parameters: {
    layout: 'centered',
  },
  loaders: [
    async () => {
      // Visual-regression baselines must not depend on the host's OS/
      // container locale. i18next-browser-languagedetector falls back to
      // `navigator.language`, which on Windows/macOS mirrors the machine's
      // OS display language (e.g. a Russian-language dev box) instead of a
      // fixed value — a minimal Linux container happens to default to
      // 'en-US', but that's an accident of the container, not something
      // pinned. That mismatch is exactly what let committed baselines flip
      // between English and Russian depending on which machine generated
      // them. Pin it explicitly here; this only affects Storybook — the
      // real app's language detection (src/shared/lib/i18n/index.ts) is
      // untouched.
      await i18n.changeLanguage('en');
      return {};
    },
  ],
  decorators: [
    withThemeByClassName({
      themes: {
        light: '',
        dark: 'dark',
      },
      defaultTheme: 'light',
      parentSelector: 'html',
    }),
    (Story) => <QueryClientProvider client={queryClient}>{Story()}</QueryClientProvider>,
  ],
};

export default preview;
