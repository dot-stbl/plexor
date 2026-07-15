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
    port: 5173,
    strictPort: true,
    cors: true,
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