# Routing — TanStack Router file conventions

> **TL;DR:** A flat route file like `vms.new.tsx` creates a **nested child
> route** of `vms.tsx`. The parent must have `<Outlet />` for the child
> to render. Without that, the child route exists but is invisible.
> For "sibling" pages under the same URL prefix, use **directory routes**
> instead: `vms/index.tsx` + `vms/new.tsx`.

## When to use what

Plexor uses TanStack Router with file-based routing. The plugin
(`@tanstack/router-plugin/vite`) watches `src/routes/` and regenerates
`src/routeTree.gen.ts` on every build.

| You want... | Use this structure |
|---|---|
| A standalone page (e.g. `/vms`) | `routes/vms.tsx` |
| A standalone page at a nested path (e.g. `/vms/new`) | `routes/vms/new.tsx` (**directory**, not flat) |
| A layout that wraps nested children (e.g. settings shell with `settings.profile`, `settings.security`) | `routes/settings.tsx` (with `<Outlet />`) + `routes/settings/profile.tsx` + `routes/settings/security.tsx` |
| A page with a single dynamic segment (e.g. `/clusters/$id`) | `routes/clusters/$id.tsx` |
| A page nested under a dynamic segment (e.g. `/clusters/$id/nodes/new`) | `routes/clusters/$id/nodes/new.tsx` |

## Why this matters

The flat-route convention `a.b.tsx` = "b is a child of a" catches people
out. `routes/vms.tsx` + `routes/vms.new.tsx` makes `/vms/new` a child
of `/vms`. If `vms.tsx` (the list page) doesn't render `<Outlet />`,
the create page exists in the route tree but renders into nothing
visible. The URL changes, the page looks the same.

Two ways to fix this:

1. **Add `<Outlet />` to the parent page** — only correct if the
   parent is genuinely a layout (chrome around a child page). For VM
   list + VM create, it's not — they're parallel pages.
2. **Convert to directory routes** — `routes/vms/index.tsx` +
   `routes/vms/new.tsx`. Both are children of `rootRouteImport`,
   rendered independently. No `<Outlet />` needed.

We chose option 2 because the list and create pages are **parallel
resources under the `/vms/` URL namespace**, not a layout/content
pair.

## The Plexor convention

| Path | File | Notes |
|---|---|---|
| `/` | `routes/index.tsx` | Root index |
| `/vms` | `routes/vms/index.tsx` | List page (sibling) |
| `/vms/new` | `routes/vms/new.tsx` | Create page (sibling) |
| `/clusters` | `routes/clusters/index.tsx` | List (sibling) |
| `/clusters/$id` | `routes/clusters/$id.tsx` | Detail (sibling) |
| `/clusters/$id/nodes/new` | `routes/clusters/$id/nodes/new.tsx` | Nested under detail (true child) |
| `/networks` | `routes/networks.tsx` | Standalone |
| `/audit` | `routes/audit.tsx` | Standalone |
| `/billing` | `routes/billing.tsx` | Standalone |
| `/components` | `routes/components.tsx` | Standalone (storybook) |

Each top-level resource gets a directory (`<resource>/`) with sibling
pages inside. Sub-resources that genuinely nest (e.g. nodes
under a cluster) go deeper into the directory tree.

## What this means for new screens

When adding a new resource (e.g. Volumes):

1. Create `routes/volumes/index.tsx` for the list page
2. Create `routes/volumes/new.tsx` for the create form
3. Add `/volumes` and `/volumes/new` to `AppRoute` in
   `src/shared/ui/app-shell/nav-config.tsx`
4. Run `bun run build` to regenerate `routeTree.gen.ts`
5. Both routes appear in the type union automatically

## What NOT to do

```tsx
// ❌ DON'T: nested flat-route without <Outlet />
// routes/vms.tsx — renders the list, no <Outlet />
// routes/vms.new.tsx — child of vms, gets nested, but invisible
// User sees: URL changes to /vms/new, but the page looks like /vms
```

```tsx
// ❌ DON'T: <Outlet /> in a non-layout page
// routes/vms.tsx
function VmsPage() {
  return (
    <main>
      <PageHeader ... />
      <DataTable ... />
      <Outlet />  // ← makes /vms a layout, not a page
    </main>
  )
}
```

```tsx
// ✅ DO: directory routes for parallel pages
// routes/vms/index.tsx — /vms (sibling)
// routes/vms/new.tsx   — /vms/new (sibling)
// Both children of rootRouteImport, rendered independently.
```

## Verifying the route tree

`src/routeTree.gen.ts` is auto-generated. After every `bun run build`,
open it and look for the `getParentRoute` line on each new route:

```ts
const VmsIndexRoute = VmsIndexRouteImport.update({
  id: '/vms/',          // ← trailing slash is a directory route marker
  path: '/vms/',
  getParentRoute: () => rootRouteImport,  // ← good: top-level
})

const VmsNewRoute = VmsNewRouteImport.update({
  id: '/vms/new',
  path: '/vms/new',
  getParentRoute: () => rootRouteImport,  // ← good: top-level (sibling of vms)
})
```

If `getParentRoute` is `() => SomeOtherRoute` for a page that should
be standalone, you have a nested child. Either add `<Outlet />` to
the parent (if it's a real layout) or convert to directory routes
(if the two pages are parallel).

## The plugin and config

The TanStack Router plugin is configured in `vite.config.ts`:

```ts
TanStackRouterVite({
  routesDirectory: './src/routes',
  generatedRouteTree: './src/routeTree.gen.ts',
})
```

Watch behavior: the plugin regenerates `routeTree.gen.ts` on every
Vite dev/build. File additions, deletions, and renames all trigger
a regen. The dev server picks up the new route types immediately
(no restart needed for the route file itself — but `appType: 'spa'`
in `vite.config.ts` requires a one-time dev-server restart to take
effect on the very first run).

## Navigation

Three ways to navigate between routes:

```tsx
// 1. <Link> — declarative, SPA navigation via History API
import { Link } from '@tanstack/react/router';
<Link to="/vms/new">Создать ВМ</Link>

// 2. <Button onClick={navigate}> — programmatic, SPA navigation
import { useNavigate } from '@tanstack/react/router';
function MyComponent() {
  const navigate = useNavigate();
  return (
    <Button onClick={() => navigate({ to: '/vms/new' })}>
      Создать ВМ
    </Button>
  );
}

// 3. window.location.href = '/path' — full page reload
// Last resort. Use only when SPA navigation breaks for some reason
// (e.g. dev server doesn't serve index.html for unknown routes).
```

Rule 1 (Link) and Rule 2 (useNavigate) are equivalent in behavior
— both use the History API and don't trigger network requests. The
button should always be Rule 2 (Link inside a button is a button
inside an anchor, which is invalid HTML and fires a Base UI warning).

## Common pitfalls

1. **`<Button render={<Link>}` triggers a Base UI warning** — Base UI
   Button's `nativeButton: true` default rejects non-button elements
   in the `render` prop. Use `<Button onClick={navigate}>` or
   `<Link nativeButton={false}>` instead.

2. **Forgetting to restart the dev server after `appType: 'spa'`** —
   `appType: 'spa'` is a Vite 6 option that's read at server creation.
   Vite watches the config file but doesn't always re-apply it. If
   the dev server returns 404 for unknown routes, restart with
   `Ctrl+C` + `bun run dev`.

3. **Forgetting to regenerate `routeTree.gen.ts`** — the plugin does
   this on `bun run build`. If you add a new file and see a type
   error like `Type '"/new/route"' is not assignable to parameter of
   type 'keyof FileRoutesByPath'`, run `bun run build` to regen.

4. **Putting pages inside a directory without an index file** — if
   you create `routes/vms/create.tsx` but no `routes/vms/index.tsx`,
   the `/vms` URL won't match anything. Add an index file or the
   URL will 404.

## When to ask for help

If a route is "weird" (e.g. renders the wrong component, double
renders, doesn't match at all), check `routeTree.gen.ts` first. The
generated file is the source of truth — the route tree is a tree,
not a bag, and parent/child relationships determine what renders
where.

If `routeTree.gen.ts` looks right but the page still doesn't work,
the bug is in the component or the navigation handler, not the
routing. Add a `console.log` in the route's component to verify
it's being entered; add a `console.log` in the `onClick` to verify
the handler fires.
