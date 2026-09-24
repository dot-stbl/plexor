# Deployment templates — `@plexor/www`

Copy-and-edit templates for hosting the static `dist/` from
`bun run build:www`. **Not** committed production config — once the
operator picks a host, the working config lands in the deploy / ops
repo. See [`README.md` §Deployment](../README.md#deployment).

## nginx

Minimal server block — SPA fallback + correct caching for hashed
`/assets/*` vs the never-cached shell:

```nginx
server {
  listen 443 ssl http2;
  server_name plexor.stbl.space;
  root /var/www/plexor-www/dist;
  index index.html;

  # SPA fallback: every URL → /index.html (TanStack Router SPA).
  location / {
    try_files $uri $uri/ /index.html;
  }

  # Long-cache hashed assets.
  location /assets/ {
    expires 1y;
    add_header Cache-Control "public, max-age=31536000, immutable";
    access_log off;
  }

  # Never cache the shell.
  location = /index.html {
    add_header Cache-Control "no-cache" always;
  }

  # TLS, HSTS, etc. — fill from your cert provisioning.
}
```

## Dockerfile

Two-stage build with `bun:1.3.10` (matches local + CI), serve via
`nginx:1.27-alpine`:

```dockerfile
FROM oven-sh/bun:1.3.10 AS build
WORKDIR /repo
COPY package.json bun.lock* ./
COPY web web
RUN cd web && bun install --frozen-lockfile
RUN bun run build:www

FROM nginx:1.27-alpine
COPY --from=build /repo/web/apps/www/dist /usr/share/nginx/html
COPY <<'EOF' /etc/nginx/conf.d/default.conf
server {
  listen 80;
  server_name _;
  root /usr/share/nginx/html;
  index index.html;
  location / { try_files $uri $uri/ /index.html; }
  location /assets/ {
    expires 1y;
    add_header Cache-Control "public, max-age=31536000, immutable";
  }
  location = /index.html { add_header Cache-Control "no-cache"; }
}
EOF
```

## Cache headers — why

| Path | Header | Why |
|---|---|---|
| `/assets/*` | `public, max-age=31536000, immutable` | Vite content-hashes the filenames — `assets/index-AbCdEf.js` is immutable; safe to cache for a year. |
| `/index.html` | `no-cache` | The shell. Always revalidate so a deploy reaches users within seconds, not within a year. |
| Other (no rule) | default | Static-ish but versioned by deploy — `no-cache` would also work; the operator picks. |

## SPA fallback — why

TanStack Router is a **client-side** SPA. The server has no knowledge
of `/docs/getting-started/install/iso/` — that URL only exists after
JS hydrates. Without `try_files ... /index.html`, nginx returns 404
on any direct deep link. Every static SPA host needs this rule
(equivalent on Caddy, S3, etc.).
