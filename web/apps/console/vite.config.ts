import { defineConfig } from 'vite';
import react from '@vitejs/plugin-react';
import tailwindcss from '@tailwindcss/vite';
import { TanStackRouterVite } from '@tanstack/router-plugin/vite';
import path from 'node:path';

export default defineConfig({
  plugins: [
    // TanStack Router file-based code-gen — generates routeTree.gen.ts
    TanStackRouterVite({
      routesDirectory: './src/routes',
      generatedRouteTree: './src/routeTree.gen.ts',
      // Skip co-located `*.test.{tsx,ts}` and `*.stories.tsx` files so
      // component tests + Storybook stories don't get pulled into the
      // route tree at build / test time. Stories live in `routes/` next
      // to their routes (per page-authoring.md) but never export `Route`,
      // so the plugin warns and exits 1 without this filter.
      routeFileIgnorePattern: '\\.(test|stories)\\.(tsx|ts)$',
    }),
    react(),
    tailwindcss(),
  ],
  resolve: {
    alias: {
      '@': path.resolve(__dirname, './src'),
    },
  },
  optimizeDeps: {
    // kubb plugin-client declares axios as optional peer dep; pre-bundle
    // explicitly so Vite resolves axios at startup instead of throwing
    // 'Could not resolve axios' at first import. Path is the kubb 4.x
    // subpath (3.x had `./client`; 4.x split into `./clients/axios` and
    // `./clients/fetch`). Generated code imports `clients/axios`.
    include: ['@kubb/plugin-client/clients/axios', 'axios'],
  },
  server: {
    host: '0.0.0.0',
    port: 17100,
    strictPort: true,
    cors: true,
  },
  // Same host-agnostic bind for `vite preview` of the built `dist/`.
  // Preview is what nginx will proxy to in production-like local testing.
  preview: {
    host: '0.0.0.0',
    port: 17110,
    strictPort: true,
  },
  // SPA fallback — TanStack Router uses History API navigation, so
  // direct hits on /vms/new, /clusters/$id etc. need to be served
  // index.html so the client-side router can take over. Without this
  // the dev server returns 404 for unknown paths. Requires dev-server
  // restart to take effect.
  appType: 'spa',
  build: {
    outDir: 'dist',
    sourcemap: true,
    rollupOptions: {
      // Exclude the kubb-generated API client from the build until the
      // codegen pipeline is finalized (depends on @kubb/plugin-client +
      // axios which aren't fully wired yet). Safe to remove once the
      // generated client is consumed by the showcase.
      external: [/^\/src\/shared\/api\/src\//],
    },
  },
});