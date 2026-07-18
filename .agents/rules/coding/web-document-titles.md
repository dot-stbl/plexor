---
description: every Plexor web route MUST set its own `document.title` via `routeHead()` (static) or `useDocumentTitle()` (dynamic); brand name `plexor` is centralized in `APP_NAME` constant and lowercase
globs:
  - "web/apps/console/src/routes/**/*.tsx"
  - "web/apps/console/src/shared/lib/{app-name,route-head,use-document-title}.ts"
  - "web/apps/console/index.html"
always: true
---

# Web document titles — `routeHead()` + `useDocumentTitle()`

Every route in `web/apps/console/src/routes/` MUST declare its own
`<title>` via one of two helpers in `src/shared/lib/`. Hardcoded titles,
missing titles, or titles with the wrong brand are a bug.

## Why

- Browser tabs show the page title — user needs to know which tab is which.
- Social previews / bookmarks / PWA install banners use the title.
- The brand name `plexor` (lowercase) must be consistent across every
  surface (HTML, OG cards, browser tab, PWA install name).
- Renaming the brand should be a one-line change. Hardcoded strings
  scattered across files make it a grep-and-pray.

## Single source of truth — `APP_NAME`

All titles use the `APP_NAME` constant. Never hardcode `"plexor"` (or
`"Plexor"`, or any variant) anywhere a title is set:

```ts
// src/shared/lib/app-name.ts
export const APP_NAME = 'plexor';
```

If the brand name changes, change here. One file, one constant.

## Static titles — `routeHead()`

For routes whose title is fully known at definition time (no data load,
no per-record variation), spread the helper into the route config:

```tsx
import { routeHead } from '@/shared/lib/route-head';

export const Route = createFileRoute('/vms/')({
  component: VmsPage,
  ...routeHead('VMs'),     // → <title>vms · plexor</title>
});
```

The helper:
- Lowercases the page argument (`'VMs'` → `'vms'`)
- Appends ` · ${APP_NAME}`
- Returns `{ head: () => ({ title }) }` for TanStack Router's `head()`

Special case — the home page or any page where the brand alone is the
right title:

```tsx
export const Route = createFileRoute('/')({
  component: HomePage,
  ...routeHead(null),       // → <title>plexor</title>
});
```

## Dynamic titles — `useDocumentTitle()`

For routes whose page-specific part of the title comes from loaded data
(cluster name from `useGetCluster(id)`, VM name from `useVm(id)`, etc.),
call the hook inside the component:

```tsx
import { useDocumentTitle } from '@/shared/lib/use-document-title';

function ClusterDetailPage() {
  const { cluster } = useGetCluster(id);
  useDocumentTitle(cluster?.name ?? null);
  // → <title>prod-eu-1 · plexor</title> once cluster loads
  // → <title>plexor</title> while loading / on error
}
```

The hook:
- Sets `document.title` on mount and every change to `page`
- Restores the previous title on unmount (avoids stale titles when navigating away)
- Lowercases the page argument before composing

Use this only when the title cannot be known at route-definition time.
For a list page where the title is just `"VMs"`, use `routeHead()` —
it runs before any data loads, so the tab title is correct immediately.

## `index.html` — initial / fallback title

The static `<title>` in `index.html` is the fallback shown **before**
the React app boots (initial HTML response, before JS executes). It
must:

- Equal `APP_NAME` exactly (lowercase — no `Plexor`, no `PLEXOR`)
- Match `og:title`, `twitter:title`, `apple-mobile-web-app-title`,
  `application-name` (all the same string)

```html
<meta name="application-name" content="plexor" />
<meta name="apple-mobile-web-app-title" content="plexor" />
<meta property="og:site_name" content="plexor" />
<meta property="og:title" content="plexor" />
<meta name="twitter:title" content="plexor" />
<title>plexor</title>
```

If the brand renames, all of these move together via `APP_NAME`.
For OG images (which can't be templated), the asset itself must be
regenerated.

## Layout routes (`route.tsx`) — no head needed

Layout files like `vms/route.tsx`, `clusters/route.tsx`, etc., are
parents that just render `<Outlet />`. They don't have a meaningful
title of their own — skip `routeHead()` on them. The leaf route
(`vms/index.tsx`, `clusters/$id.tsx`) is where the title lives.

```tsx
// vms/route.tsx — layout, no head
export const Route = createFileRoute('/vms')({
  staticData: { crumb: 'Virtual machines' },
  component: () => <Outlet />,
});

// vms/index.tsx — leaf, has head
export const Route = createFileRoute('/vms/')({
  component: VmsPage,
  ...routeHead('VMs'),
});
```

## Anti-patterns

- ❌ `document.title = 'Plexor'` — hardcoded brand string, won't survive
  rename; should use the helper or `APP_NAME`
- ❌ `<title>Plexor</title>` in `index.html` (capitalized) — inconsistent
  with the lowercase brand
- ❌ No `routeHead()` / `useDocumentTitle()` on a new route — browser
  tab shows the previous page's title or the static fallback
- ❌ `routeHead('VMs · plexor')` — the helper already appends
  ` · ${APP_NAME}`; double-up = `"vms · plexor · plexor"`
- ❌ `routeHead('New Kubernetes cluster')` is fine, but if you write
  `routeHead('NEW KUBERNETES CLUSTER')` the result is correct (lowercased)
  but the source is shouting. Lowercase in source.
- ❌ Adding `useDocumentTitle()` to a layout (`route.tsx`) — layouts
  unmount/remount on child navigation, causing flicker. Put it on the
  leaf route instead.

## Self-audit grep

Run these from the plexor repo root before committing a web change:

```bash
# 1. Routes with createFileRoute but no routeHead / no useDocumentTitle
#    (catches new routes that forgot to declare a title).
rg -nL "createFileRoute\(" web/apps/console/src/routes/ \
    | rg -v "route.tsx\$|__root.tsx\$" \
    | xargs rg -l "createFileRoute" \
    | xargs rg -L "routeHead|useDocumentTitle"

# 2. Hardcoded brand strings in titles — should be ZERO matches
#    (everything goes through APP_NAME).
rg -nE "document\.title\s*=\s*['\"](plexor|Plexor|PLEXOR)" web/apps/console/src/

# 3. Title in index.html — must match APP_NAME (case-sensitive).
#    If you change APP_NAME, change these in lockstep.
rg -n "<title>" web/apps/console/index.html

# 4. Title strings in route configs that don't go through the helper.
rg -nE "title:\s*['\"](plexor|Plexor)" web/apps/console/src/routes/
```

A non-empty result from #1 means a new route has no title — fix it
before commit. Non-empty #2/#3/#4 means a hardcoded brand string
escaped the helper — replace with `routeHead()` / `useDocumentTitle()`
or `APP_NAME`.

## Related

- `BRAND.md` (in `dot-stbl/brand`) — brand voice, lockup pattern, naming
- `coding/naming-and-types.md` — naming conventions for code in general
- `coding/code-shape.md` — pattern matching, var, file-static constants
  (the constant-class pattern `APP_NAME` follows)