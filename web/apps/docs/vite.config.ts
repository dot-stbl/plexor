import { defineConfig } from 'vite';
import react from '@vitejs/plugin-react';
import tailwindcss from '@tailwindcss/vite';
import mdx from '@mdx-js/rollup';
import { TanStackRouterVite } from '@tanstack/router-plugin/vite';
import remarkGfm from 'remark-gfm';
import remarkFrontmatter from 'remark-frontmatter';
import rehypeSlug from 'rehype-slug';
import rehypeAutolinkHeadings from 'rehype-autolink-headings';
import path from 'node:path';

export default defineConfig({
  // Static site served from any origin — root-relative asset paths so the
  // built `dist/` is host-agnostic and ready for nginx / Caddy reverse-proxy
  // when the docs site gets its own vhost (plexor.stbl.space, future). Don't
  // change this to a sub-path without also updating asset URLs in index.html.
  base: '/',
  plugins: [
    TanStackRouterVite({
      routesDirectory: './src/routes',
      generatedRouteTree: './src/routeTree.gen.ts',
      routeFileIgnorePattern: '\\.test\\.(tsx|ts|mdx)$',
    }),
    react(),
    mdx({
      remarkPlugins: [remarkGfm, remarkFrontmatter],
      rehypePlugins: [
        rehypeSlug,
        [rehypeAutolinkHeadings, { behavior: 'wrap' }],
      ],
      providerImportSource: '@mdx-js/react',
    }),
    tailwindcss(),
  ],
  resolve: {
    alias: {
      '@': path.resolve(__dirname, './src'),
    },
  },
  server: {
    // Bind to all interfaces so the dev server is reachable from any host
    // header (LAN testing, containerised runs, eventual nginx upstream).
    // Localhost browsing still works — `0.0.0.0` listens on loopback too.
    host: '0.0.0.0',
    port: 17101,
    strictPort: true,
    cors: true,
  },
  // Same host-agnostic bind for `vite preview` of the built `dist/`.
  // Preview is what nginx will proxy to in production-like local testing.
  preview: {
    host: '0.0.0.0',
    port: 17111,
    strictPort: true,
  },
  appType: 'spa',
  build: {
    outDir: 'dist',
    sourcemap: true,
  },
});