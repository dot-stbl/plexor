import { createMDX } from 'fumadocs-mdx/next';

const withMDX = createMDX();

/**
 * Static export into `out/`, served by the dedicated nginx image
 * (deploy/docker/Dockerfile.docs) at the ROOT of docs.anlytra.stbl.space
 * (and docs.anlytra.vironima.internal) — no basePath:
 *
 * - internal route `/`        -> https://docs.anlytra.stbl.space/       (RU, default locale at the root)
 * - internal route `/en/...`  -> https://docs.anlytra.stbl.space/en/... (EN locale)
 *
 * `trailingSlash: true` makes Next emit every page as `page/index.html`,
 * which nginx maps back to `/page/` via try_files ($uri/ then
 * $uri/index.html). Fumadocs generates all internal links in the
 * trailing-slash form, so in-app navigation never relies on redirects.
 */
/** @type {import('next').NextConfig} */
const config = {
  output: 'export',
  trailingSlash: true,
  reactStrictMode: true,
};

export default withMDX(config);
