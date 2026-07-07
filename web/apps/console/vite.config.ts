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
  server: {
    port: 5173,
    strictPort: true,
    cors: true,
  },
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