import { defineConfig, type Connect } from 'vite';
import react from '@vitejs/plugin-react';
import tailwindcss from '@tailwindcss/vite';
import { TanStackRouterVite } from '@tanstack/router-plugin/vite';
import path from 'node:path';
import fs from 'node:fs';
import { fileURLToPath } from 'node:url';

const __dirname = path.dirname(fileURLToPath(import.meta.url));

/**
 * SPA fallback middleware — serves index.html for any GET request that
 * isn't a Vite internal, an API path, or a static asset. Required
 * because TanStack Router uses History API navigation and direct hits
 * on /vms/new, /clusters/$id etc. need to load the SPA shell.
 *
 * Vite 6 has `appType: 'spa'` for this, but the option only takes
 * effect after a dev-server restart. This middleware is a hard
 * guarantee: it intercepts the request at the connect layer, before
 * Vite's static-file resolution, and forces index.html for any
 * path that doesn't look like an asset or API call.
 */
function spaFallback(): import('vite').Plugin {
  return {
    name: 'plexor-spa-fallback',
    configureServer(server) {
      const indexHtml = fs.readFileSync(path.resolve(__dirname, 'index.html'), 'utf-8');
      const middleware: Connect.NextHandleFunction = (req, res, next) => {
        if (req.method !== 'GET') return next();

        const url = req.url ?? '/';

        // Pass through: Vite internals (HMR, source files, modules).
        if (url.startsWith('/@')) return next();
        if (url.startsWith('/node_modules/')) return next();
        if (url.startsWith('/src/')) return next();

        // Pass through: API calls (MSW handles them in the service worker;
        // any real backend calls also need to reach the network).
        if (url.startsWith('/api/')) return next();

        // Pass through: static assets (anything with a file extension or
        // a hash — fonts, images, JS chunks, source maps).
        if (/\.[a-z0-9]{2,8}($|\?)/i.test(url)) return next();

        // Pass through: MSW service worker (registered as /mockServiceWorker.js).
        if (url === '/mockServiceWorker.js') return next();

        // Otherwise: SPA route — serve index.html.
        res.statusCode = 200;
        res.setHeader('Content-Type', 'text/html');
        res.end(indexHtml);
      };
      // Register BEFORE Vite's own middlewares so the fallback wins
      // on routes that Vite would otherwise 404.
      server.middlewares.use(middleware);
    },
    configurePreviewServer(server) {
      const indexHtml = fs.readFileSync(path.resolve(__dirname, 'dist/index.html'), 'utf-8');
      server.middlewares.use((req, res, next) => {
        if (req.method !== 'GET') return next();
        const url = req.url ?? '/';
        if (url.startsWith('/@') || url.startsWith('/node_modules/') || url.startsWith('/src/')) return next();
        if (url.startsWith('/api/')) return next();
        if (/\.[a-z0-9]{2,8}($|\?)/i.test(url)) return next();
        if (url === '/mockServiceWorker.js') return next();
        res.statusCode = 200;
        res.setHeader('Content-Type', 'text/html');
        res.end(indexHtml);
      });
    },
  };
}

export default defineConfig({
  plugins: [
    // SPA fallback runs first so it can intercept before Vite's static
    // file resolution returns 404 for unknown paths.
    spaFallback(),
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
    // 'Could not resolve axios' at first import.
    include: ['@kubb/plugin-client/client', 'axios'],
  },
  server: {
    port: 5173,
    strictPort: true,
    cors: true,
  },
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