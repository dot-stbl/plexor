# AGENTS — `@plexor/www`

> The Plexor project website. Sibling of `@plexor/console` in the
> `web/` monorepo, sharing `@plexor/ui` for the Plexor DS tokens and
> primitives. **Read this before touching anything in `web/apps/www/`.**

## 1. What this app is

The Plexor project website: marketing landing (`/`), operator-facing
documentation (`/docs/*`), and a versioned release changelog
(`/changelog`) — like nextjs.org. Same Plexor DS tokens as
`@plexor/console`, same theme presets, same icon set, same theming
primitive. Lives at `web/apps/www/`; runs as a Vite SPA on dev port
**17101** (see `web/docs/PORTS.md`).

## 2. Architecture (short)

- **Framework**: Vite 6 + React 19 + TanStack Router 1.170 (file-based
  routing, route tree auto-generated under `src/routeTree.gen.ts`; see
  §2b — this app pins a newer router than `web/apps/console`, which
  stays on 1.91).
- **MDX**: `@mdx-js/rollup` with `remark-gfm`, `remark-frontmatter`,
  `rehype-slug`, `rehype-autolink-headings` (see `vite.config.ts`).
- **Theme system**: imports `@plexor/ui/tokens.css` + applies
  `<html data-theme="…">` via `applyBootPreset()` in
  `src/lib/apply-theme.ts`; the inline script in `index.html` does the
  same synchronously before first paint (no FOUC).
- **Shared with console**: `@plexor/ui` package — brand mark, theme
  picker, design tokens. **Do not reimplement.**

## 2a. Static prerendering (SSG) — 2026-09-24

Every real route now ships **real server-rendered HTML** at build time
— `bun run build` = `vite build` (client bundle) followed by
`bun run scripts/prerender/run.ts` (prerender pass), which then
hydrates back into the same SPA client-side. This is what makes the
site indexable: a crawler hitting `/docs/getting-started/` gets the
actual page title, description, OG/canonical tags, and full text
content in the initial HTML response, not an empty `<div id="root">`.

**Chosen approach — a post-build Vite SSR script, not TanStack Start.**
Rejected alternatives and why:
- *Migrate to TanStack Start*: would own routing, the document shell,
  and the dev/build pipeline outright — far more invasive than the
  goal required, and this app's `index.html` (inline theme-boot
  script, static meta) already works well as a static shell.
- *Playwright/headless-browser snapshot prerendering*: works, but is
  much slower (spin up a real browser per route) and produces HTML
  that's a rendered snapshot rather than actual React SSR output, so
  it can't hydrate cleanly afterward. Kept as a documented fallback
  option, not built.
- **What we built**: `src/entry-server.tsx` — a second Vite-built
  entry (`vite build` with `build.ssr` pointed at it, invoked
  programmatically from `scripts/prerender/run.ts` via Vite's own `build()`
  JS API) that, for every real route, creates a `createMemoryHistory`
  router at that path, `await router.load()`s it (resolves any
  `beforeLoad`/loader work), and renders with
  `react-dom/server`'s `renderToReadableStream` (Web Streams — the
  Node-only `renderToPipeableStream` isn't available under bun's
  `react-dom/server` resolution; `renderToString` doesn't emit the
  `<!--$-->` Suspense-boundary markers hydration needs and was rejected
  after causing an even bigger version of the mismatch below).
  `run.ts` awaits `stream.allReady` (full settle, not just the shell —
  build-time SSG has no request latency to protect), injects the
  result into the built `dist/index.html` template per route, and
  writes `dist/<path>/index.html`. This mirrors Vite's own documented
  "SSR without a framework" pattern.
- **Route list**: derived from the router itself, not hand-maintained.
  `router.routesByPath` keys are trimmed (no trailing slash) but a
  route whose real `fullPath` ends in `/` always wins the overwrite
  race for that key — so `Object.values(router.routesByPath).map(r =>
  r.fullPath)`, deduped, is exactly the 45 real leaf pages (marketing,
  changelog, every `/docs/**` page), automatically staying in sync as
  pages are added.
- **Per-route head**: the router ships a `HeadContent`/`Scripts` pair
  (post-1.170 SSR API, see §2b), but we still don't use `HeadContent`
  for `<title>`/meta — it never invoked a route's `head()` option
  internally at the 1.91.0 pin this was originally built against, so
  we read it ourselves instead: `src/lib/collect-route-head.ts` walks
  matched routes calling `route.options.head?.()`; `src/lib/head-meta.ts`
  (`resolveHead`, unit-tested) merges leaf-wins title/description and
  derives canonical + Open Graph + Twitter Card tags from them — so
  **none of the 45 existing per-route `head()` calls needed to
  change**, on the 1.91.0 pin or the current one. `src/lib/use-sync-document-head.ts`
  applies the same resolved head client-side on every in-app navigation.
  Adopting `HeadContent` to replace this hand-rolled path is a possible
  follow-up, not done here (out of scope for the hydration-fix pass).
- **`SITE_URL`**: `src/components/chrome/nav-config.ts`, currently
  `https://plexor.stbl.space` (the only domain mentioned anywhere in
  the repo, `vite.config.ts`'s base-path comment) — provisional, no
  production host exists yet (§9). Used for canonical links, OG `url`,
  and `sitemap.xml`/`robots.txt`.
- **`dist/sitemap.xml` + `dist/robots.txt`**: generated by the same
  prerender pass, one `<loc>` per distinct rendered page,
  `Sitemap:` pointing back at it.
- **`dist/404.html`**: a byte-for-byte copy of the *pre-prerender*
  `dist/index.html` template (empty `#root`) — the pure-CSR fallback
  for any URL the eventual reverse proxy hasn't mapped to a real page.
  `src/main.tsx` branches on `rootElement.hasChildNodes()`:
  `hydrateRoot` for a prerendered page, plain `createRoot` for this
  fallback. **Whoever wires nginx/Caddy (§9, not done yet): point the
  SPA-fallback / custom-404 directive at `/404.html`, not
  `/index.html`** — the latter is now the real prerendered landing
  page.
- **SSR-safety fix required**: `src/components/motion/use-theme-name.ts`
  picked a different `<img>` `src` (light/dark screenshot) directly
  from `document.documentElement.dataset.theme` in its `useState`
  lazy initializer — server-safe (falls back to `'light'`) but NOT
  hydration-safe, since the client's *first* render already sees the
  real persisted theme (set by `applyBootPreset()` before
  `hydrateRoot`), a guaranteed mismatch for dark-theme users. Fixed:
  the initial state is now the fixed `'light'` default on both sides,
  corrected via `useLayoutEffect` (not `useEffect`) immediately after
  mount — synchronous, before the browser paints, so there's no
  visible flash and no mismatch.
- **No dependency added.** `react-dom/server`'s `renderToReadableStream`,
  `@tanstack/react-router`'s `createMemoryHistory`, and Vite's `build()`
  API were all already installed; only `package.json`'s `"build"`
  script changed (now chains the prerender step).

## 2b. Router upgrade + hydration-mismatch fix — 2026-09-24

`@tanstack/react-router` is now `1.170.39` **in `web/apps/www` only**
(`web/apps/console` stays pinned at `1.91.0` — bun resolves each
workspace app's version independently since neither `package.json`
pins the other; confirmed via `bun.lock`, two separate resolved
entries). `@tanstack/router-plugin`'s existing `^1.168.19` range
already resolves to a build whose own peer wants `@tanstack/react-router
^1.170.38`, so no plugin version change was needed — only the app's
own dependency bumped.

**What this fixes**: every prerendered page used to log one React
hydration-mismatch warning on first client paint. Root cause: TanStack
Router's root `<Outlet/>` unconditionally wrapped its child match in
`<Suspense fallback={null}>` (`Match.js`, `matchId === rootRouteId`)
server-side but NOT client-side — a confirmed upstream bug,
[TanStack/router#3305](https://github.com/TanStack/router/issues/3305),
fixed via [TanStack/router#4495](https://github.com/TanStack/router/pull/4495).
The fix lives in `Match.js`'s `canWrapInSuspense`: the root match skips
Suspense-wrapping only when `router.ssr` is truthy on **both** sides —
that flag is exactly what the official (non-Start) SSR primitives set.

**A prior drive-by version bump to `1.170` alone was tried and
reverted** ("silently broke SSR rendering") — root cause, now
diagnosed: the router's `trailingSlash` default (`'never'`) started
enforcing canonical-path matching, not just href generation. Every
leaf page this site prerenders is an index route whose `fullPath` ends
in `/` (`getAllRoutePaths()`); feeding that trailing-slash path
straight into `createMemoryHistory` under the new default produced
**zero route matches** (confirmed: `router.state.matches` was `[]`),
so every non-`/` route rendered a genuinely empty `appHtml` with no
thrown error. Both routers (`entry-server.tsx` and `main.tsx`) now set
`trailingSlash: 'preserve'` to match hrefs exactly as given — the
implicit behavior the whole prerender pipeline was already built
against.

**Chosen fix — official SSR primitives, not the full TanStack-Start-shaped
document-ownership rewrite.** `@tanstack/react-router/ssr/server` /
`/ssr/client` also export `RouterServer`/`RouterClient` +
`createRequestHandler`/`renderRouterToStream`, which require
`__root.tsx` to own the entire `<html>` document
(`hydrateRoot(document, <RouterClient/>)`) — that's TanStack Start's
own shape, and stays out of scope here (this app's `index.html` static
shell, `404.html` CSR fallback, and `main.tsx`'s dev-mode branch all
still work exactly as before). Instead:
- `entry-server.tsx`'s `renderRoute()` calls `attachRouterServerSsrUtils({
  router, manifest: undefined })` (from `@tanstack/react-router/ssr/server`)
  right after creating the router — this is what sets `router.ssr`
  server-side, matching what the client sets during hydration. After
  `router.load()`, it calls `await router.serverSsr.dehydrate({signal})`
  (this site has no route `loader`s, so the dehydrated payload is just
  route-match bookkeeping, no async data) and, in a `finally`, calls
  `router.serverSsr.cleanup()`. Rendering itself is unchanged —
  still our own `renderToStringWithSuspenseMarkers` via
  `renderToReadableStream` (per-route code-split Suspense boundaries
  were never the problem; only the root one was).
- `__root.tsx` renders `<Scripts/>` (from the main `@tanstack/react-router`
  export) as a plain descendant of `<Outlet/>`'s siblings inside
  `RootLayout` — it needs router context (`useRouter()`), which it gets
  by being inside the routed tree, but doesn't care that it's landing
  inside `#root` rather than at the end of a framework-owned `<body>`.
  Server-side it embeds the dehydrated `window.$_TSR = {...}` payload +
  hydration bootstrap script; client-side (no manifest, no per-route
  `scripts`) it renders nothing.
- `main.tsx`'s prerendered-page branch (`rootElement.hasChildNodes()`)
  now does `hydrateRoot(rootElement, <RouterClient router={router}/>)`
  instead of the old manual `router.load().then(...)` +
  `loadRouteChunk` preloading dance — `RouterClient`'s internal
  `hydrate()` (from `@tanstack/react-router/ssr/client`) already does
  that chunk-preloading itself, and only mounts `<RouterProvider>`
  once `router.ssr` is populated from the dehydrated payload, which is
  what keeps the two sides' Suspense-wrapping decision in sync. The
  `else` branch (dev mode, `404.html`) is untouched: plain `createRoot`
  + `<RouterProvider>`, no dehydration payload expected or required.

**Verified**: all 45 routes prerender clean (`bun run build:www`);
`bun --filter '@plexor/www' gate` green; a throwaway Playwright check
against `vite preview` (`/`, `/changelog/`, `/docs/`,
`/docs/getting-started/`, `/docs/concepts/networking/`) shows **zero**
console errors/warnings on any of them (the mismatch warning is gone),
real content in every page, client-side navigation confirmed
soft (no full reload), and `⌘K`/`Ctrl K` search opens correctly.

## 3. Project structure (relevant subtrees)

```
web/apps/www/
├── package.json              @plexor/www, workspace-internal
├── vite.config.ts             Vite + TanStack Router + MDX plugins
├── index.html                 boot HTML with inline theme script
├── scripts/agent/              shot.ts + lib/* — the visual-check tool (see §4)
├── src/
│   ├── main.tsx               RouterClient/RouterProvider + applyBootPreset() + 404
│   ├── styles.css             imports @plexor/ui/tokens.css + .docs-prose
│   ├── routeTree.gen.ts       AUTO-GENERATED by @tanstack/router-cli
│   ├── lib/
│   │   ├── mdx-components.tsx  global MDX primitives + prose typography
│   │   └── apply-theme.ts     boot preset logic (mirrors index.html)
│   ├── content/
│   │   ├── changelog/*.mdx    one file per release (see §5a)
│   │   └── search-index.ts    SEARCH_INDEX read by CommandMenu
│   ├── components/
│   │   ├── ui/                 our own primitives on react-aria-components,
│   │   │                       ported from console's `shared/ui/primitives/`
│   │   │                       (button, badge, dialog, tabs, collapsible,
│   │   │                       scroll-area, dropdown-menu*, separator, input,
│   │   │                       status-pill, copy-button). NOT shadcn, NOT
│   │   │                       base-ui — never add either; see §6a.
│   │   ├── chrome/              SiteHeader, SiteFooter, SiteFrame/FrameSection,
│   │   │                       CommandMenu (+ command-menu-store/-filter),
│   │   │                       ThemeToggle, GitHubIcon, nav-config.ts — the
│   │   │                       single source of truth for the GitHub URL,
│   │   │                       clone/run commands, and version string. Every
│   │   │                       other file reads these from `nav-config.ts`,
│   │   │                       never hardcodes them.
│   │   ├── mdx/               MDX primitives: <Callout/>, <Step/>, <Screenshot/>, <Kbd/>, <Diagram/>, <TypeTable/>, <Accordions/>, <Choices/>, <PlatformMatrix/>
│   │   ├── marketing/          landing sections: hero, install (quickstart
│   │   │   ├── bento/          terminal), preview (console mock), bento
│   │   │   └── preview/        features, spectrum, manifesto, comparison,
│   │   │                       release-callout, cta
│   │   ├── docs/               docs chrome: sidebar (+ -chapter/-chapters),
│   │   │                       toc, breadcrumb, footer, prev-next, docs-not-found
│   │   └── changelog/           changelog-list, changelog-entry, sort-entries, types
│   └── routes/
│       ├── __root.tsx          <Outlet/> + mounts <CommandMenu/> once, globally
│       ├── (marketing)/        landing chrome (SiteHeader variant="marketing" +
│       │   └── changelog/      SiteFooter); includes /changelog
│       └── (docs)/             docs chrome (SiteHeader variant="docs" + sidebar
│           └── docs/           + TOC + SiteFooter variant="docs"), URL /docs/*
│               ├── index.tsx          redirect → /docs/getting-started/
│               ├── getting-started/, concepts/, how-to/, admin/, reference/, faq/
```

There is no `components/shared/` any more — the old `theme-picker-button.tsx`
was superseded by `components/chrome/theme-toggle.tsx` (a `DropdownMenu`
listing the three presets with a checkmark on the active one, not a blind
cycling button).

## 3a. Panel system (YC-informed restyle, 2026-09-24)

The marketing landing, `/changelog` and the `/docs` entry page are a
**stack of big flat rounded panels**, not the old full-width
`SiteFrame`/`FrameSection` hairline-border rhythm (`FrameSection` is
unchanged and still backs the header, footer and the docs 3-column
grid — only these three pages moved off it). Composition primitives
live in `components/chrome/panel.tsx`:

- `PanelContainer` — mount once per page, directly inside `SiteFrame`.
  Establishes the **contained width**: ~1232px content at a 1440px
  viewport (104px gutter each side), capped at a ~1440px content width
  from 1920px up (`max-w-[1648px]` outer, `lg:px-[104px]` gutter — do
  the arithmetic before changing either number).
- `PanelStack` — vertical rhythm (small gaps; the page background shows
  through the seams, on purpose — panels never touch and never share a
  border).
- `PanelRow` — a 2-up `lg:grid-cols-2` row of panels (e.g. a light
  screenshot panel next to an inverted terminal panel), stacked below
  `lg`.
- `Panel` — one flat panel: `rounded-3xl`, a fill token, no border, no
  shadow. **Radius convention**: `rounded-3xl` (24px) is for *site
  panels/sections* only; cards nested inside a panel use `rounded-2xl`
  (16px); buttons/inputs are unaffected — they keep the DS's own
  `radius-md`/`radius-lg` scale (`components/ui/button.tsx` etc.), never
  the panel radii.
- Fill tokens — monochrome only, no brand color, no gradients:
  `card` (brightest), `muted` (soft gray), `sunken` (`bg-surface-3`, one
  step deeper — alternation only), `inverted` (`bg-foreground
  text-background`, the one high-contrast "black block" beat per page).
  Never repeat a fill on two adjacent panels.
- `EYEBROW_CLASS` — plain sentence-case eyebrow (`text-sm font-medium
  text-muted-foreground`). The old `font-mono uppercase
  tracking-[0.16em]` eyebrow is retired **on these three pages only** —
  docs content pages and the console keep whatever their own
  conventions already are.
- **Inverted-panel buttons**: a `Button variant="default"` (`bg-primary`)
  or `variant="outline"` (`border-border`) is tuned for a light page and
  reads as near-invisible on an `inverted` panel in at least one theme.
  Use `INVERTED_BUTTON_FILLED_CLASS` / `INVERTED_BUTTON_OUTLINE_CLASS`
  (also exported from `panel.tsx`) instead — they flip to the page's own
  background/foreground pair, which is always the correct contrasting
  direction in both light and dark.
- **Illustrations**: flat geometric SVG compositions (server/chip/
  network/disk built from simple rounded shapes), colored via
  `currentColor` / Plexor DS tokens only — no particles, no glow, no
  decorative gradients, no animation beyond a subtle hover. One in the
  hero, 2–3 more across the feature/scenario panels. No stock/data-center
  photography (Plexor doesn't own a data center to photograph honestly).

## 4. Dev workflow

| Script | What it does |
|---|---|
| `bun run dev` (alias `dev:www` at root) | Vite dev on **port 17101** |
| `bun run build` (alias `build:www`) | `vite build` + prerender pass (see §2a) — static, indexable `dist/` |
| `bun run preview` (alias `preview:www`) | Vite preview on **port 17111** |
| `bun run typecheck` (alias `typecheck:www`) | `tsc --noEmit` |
| `bun run lint` (alias `lint:www`) | eslint `--max-warnings 0` |
| `bun run test` (alias `test:www`) | `vitest run` (real tests — see §4a) |
| `bun run gate` | typecheck + lint + test, one command |
| `bun run shot routes` | lists every registered route |
| `bun run shot page <path...> [--theme light\|dark\|both] [--mobile] [--full]` | headless-renders each path, screenshots it to `.shots/page/<slug>.<theme>[.mobile].png`, and writes a `.md` report (console warnings/errors, horizontal-scroll, clipped text, icon-only controls without a label, broken images) with a PASS/WARN/FAIL verdict. Summary in `.shots/LAST.md`. Spins up its own scratch Vite server on **port 17111** and kills it on exit — safe to run without a standing dev server. |

The dev server is **already running on :17101** from a prior user
action — do not start a competing one (see global
`agent-runtime-safety.md`: long-lived processes kill the parent agent).
`bun run shot` is the exception: it's the sanctioned way to render+verify
a page without a standing dev server, and it cleans up after itself.

### 4a. Tests

`bun run gate` fails on "No test files found" — every PR needs at least
one real `vitest` spec. Existing pattern: colocate `xyz.test.ts` next to
the pure helper it covers (`command-menu-filter.test.ts`,
`sort-entries.test.ts`, `docs-chapters.test.ts`, `docs-prev-next.test.ts`,
`bento-data.test.ts`, `search-index.test.ts`, …). Prefer testing the
extracted pure function over the component that renders it.

### Adding a new docs page

1. Create `src/routes/(docs)/docs/<chapter>/<slug>/index.tsx` +
   `content.mdx`. Mirror the FAQ pattern in
   `src/routes/(docs)/docs/faq/troubleshooting/index.tsx`.
2. Register the slug in `CHAPTERS` in
   `src/components/docs/docs-sidebar.tsx`.
3. Update the parent chapter's `index.tsx` + `content.mdx` to link to
   the new page (use TanStack Router `<Link to=…>` — never `<a>`).
4. Run `bun run build` to regenerate `src/routeTree.gen.ts`.

## 5. MDX primitives

Existing primitives (all in `src/components/mdx/`):
`Callout`, `Step`, `Screenshot` (+ `ScreenshotPlaceholder`),
`Diagram` (+ `DiagramPlaceholder`), `Kbd`, `TypeTable` (+ `TypeRow`),
`Accordions` (+ `Accordion`), `Choices` (+ `Choice`),
`PlatformMatrix`. All registered globally in
`src/lib/mdx-components.tsx` so MDX files use them without imports.

**Add a new primitive**:
1. Create `src/components/mdx/xyz.tsx` (Plexor DS tokens only,
   ≤ 150 LOC, under the file-length cap of 200).
2. Add `'use client'` if it needs interactivity.
3. Re-export from `src/components/mdx/index.ts` barrel.
4. Register in `getMdxComponents()` in `src/lib/mdx-components.tsx`.

### 5a. Changelog content contract

One MDX file per release under `src/content/changelog/*.mdx`. Frontmatter
(typed as `ChangelogFrontmatter` in `src/components/changelog/types.ts`):

```yaml
---
version: "v0.2"        # parsed numerically for sort order — not filename order
title: "Compute, network and storage"
status: "shipped"      # "shipped" | "next" | "design" — StatusPill vocabulary
bullets:                # the single authored copy of this release's bullets;
  - "…"                 # the MDX body renders frontmatter.bullets directly,
  - "…"                 # so both the changelog list and the landing page's
---                     # release callout read the same strings.
```

No fabricated calendar dates — there are no release tags yet, so
`ChangelogList` (`src/components/changelog/changelog-list.tsx`) orders
entries by version number only, newest first. `MarketingReleaseCallout`
reads the same `import.meta.glob('/src/content/changelog/*.mdx')` and
shows the newest entry's title/status/first bullets on the landing page.

## 6. Theming

- Three presets defined in `web/packages/ui/src/themes/presets.ts`:
  `plexor-default-light`, `plexor-default-dark`, `plexor-noir`.
- Each preset exports `applyPreset(preset)` which writes CSS custom
  properties on `<html data-theme="…">`. `getPreset(id)` resolves the
  preset; `presets[]` is the ordered cycle.
- `applyBootPreset()` in `src/lib/apply-theme.ts` reads the persisted
  choice (or falls back to `presets[0]`) and applies it on first React
  paint.
- Inline boot script in `index.html` does the same synchronously
  before any module loads (no-FOUC). Both share the localStorage key
  `plexor-theme` — keep them in sync.
- The theme control lives in chrome now:
  `src/components/chrome/theme-toggle.tsx` renders a `DropdownMenu`
  (ported `components/ui/dropdown-menu`) listing the three presets with a
  checkmark on the active one — not the old blind-cycling icon button.
  It reads preset data + persistence from `@plexor/ui`'s
  `useThemePicker()`; no preset logic is duplicated locally.

### 6a. UI primitives — `components/ui/`

Ported from `web/apps/console/src/shared/ui/primitives/`, built on
**react-aria-components** (RAC) — despite some in-code comments saying
"base-ui compat", RAC is the actual runtime; base-ui-shaped props
(`open`/`onOpenChange`, `render={<Element/>}`) are a compatibility shim
over it. **Never add shadcn or `@base-ui-components/react`** — port from
console's RAC-backed primitives instead, verbatim where possible.

Two gotchas worth knowing before touching a trigger:
- `<Button render={<a/>}>`: only `className`+`children` merge onto the
  render target — put `href`/`aria-label` on the render element itself.
- `<DropdownMenuTrigger render={<Button/>}>`: the icon must be the
  Trigger's own child, not nested inside `render`. Also: the trigger's
  cloned target is rendered directly (no `<Pressable>` wrapper) when it's
  a real element — `Pressable` is reserved for the raw-`<button>`
  fallback. Wrapping an already-RAC `Button` in `Pressable` on top of
  `MenuTrigger`'s own `PressResponder` context is redundant and trips a
  false-positive react-aria dev warning whenever the trigger is
  conditionally hidden via a responsive class (e.g. `md:hidden`) at
  mount. See `components/ui/dropdown-menu-trigger.tsx`.

## 7. Routing — 404 + redirects

- Router-level `defaultNotFoundComponent: DocsNotFound` is set on
  `createRouter(...)` in `src/main.tsx`. Any unmatched URL renders
  `src/components/docs/docs-not-found.tsx` (centered, two CTAs).
- Per-route `notFoundComponent: DocsNotFound` is set on
  `src/routes/(docs)/route.tsx`. Any unmatched `/docs/...` keeps the
  docs chrome.
- `src/routes/(docs)/docs/index.tsx` redirects `/docs` and `/docs/` →
  `/docs/getting-started/` via `beforeLoad` (fires before first paint,
  no flash).

## 8. Hard rules

- **NO dev server.** Long-lived processes kill the parent agent. The
  operator starts the dev server. (See global `agent-runtime-safety.md`.)
- **Plexor DS tokens only.** `bg-card`, `border-border`,
  `text-muted-foreground`, `text-muted-2`, the `ok`/`err`/`warn`/`idle`
  status tokens, etc. — no new colours, spacing, or fonts, no hex/`oklch()`
  literals, no gradients or glow effects.
- **No 3rd-party icon dep.** Use
  `@nine-thirty-five/material-symbols-react/rounded/700` (already a
  workspace dep).
- **TanStack Router `<Link>`** for in-app navigation; raw `<a>` only
  for external URLs.
- **No internal names in user copy.** `.NET` types, schema names
  (`sigil`, `realm`, `atlas`, …), module paths (`Plexor.Modules.*`) —
  none of those belong in operator-facing content.
- **File-length cap: 200 LOC.** Split when a file grows past that (e.g.
  `components/ui/dropdown-menu.tsx` is a barrel re-exporting
  `dropdown-menu-{root,trigger,content,item,sub}.tsx` +
  `dropdown-menu-shared.ts` — the public import path
  `@/components/ui/dropdown-menu` doesn't change). **Tooling exception:**
  `scripts/agent/lib/*.ts` (the shot tool's internals) may exceed 200 LOC —
  it's a dev-time verification script, not shipped app code; still split
  it when a change makes a file meaningfully harder to navigate, but
  don't treat the cap as a hard gate there.
- **TypeScript strict.** No `any`, no `as`, no `@ts-ignore` — except the
  small, already-reviewed set of `eslint-disable-next-line` escapes inside
  the ported `components/ui/*` primitives, where RAC's own types don't
  express the base-ui-shaped compatibility props. Don't add new ones
  outside that shim layer without the same justification.
- **Operator voice.** Never talk down to the reader. No marketing
  fluff. No jargon dumps. State *what it does and when to use it*;
  see `web/docs/CONTENT-PLAN.md` for the full voice rules.

## 9. Deploy

Static build to `dist/` — every real route prerendered to its own
`index.html` (see §2a), plus `sitemap.xml` and `robots.txt`.
`vite.config.ts` sets `base: '/'` so the build is host-agnostic and
ready for nginx / Caddy reverse-proxy. Production hosting is **not yet
set up** in this repo — no nginx config, no Dockerfile, no CI job. The
operator deploys behind their own reverse proxy when ready — when they
do, point the SPA-fallback / custom-404 directive at **`/404.html`**,
not `/index.html` (§2a — the latter is now the real prerendered
landing page, not an empty shell).

## 10. Where to look in the rest of the monorepo

- `web/README.md` — workspace-level overview, gate, codegen.
- `web/apps/console/README.md` — sibling app, mock mode, kubb.
- `web/docs/CONTENT-PLAN.md` — full page inventory per chapter,
  voice rules, page boundaries.
- `web/docs/PORTS.md` — dev port pool (17100–17199); www uses
  **17101** (dev) and **17111** (preview).
- `web/packages/ui/src/themes/presets.ts` — single source of truth
  for theme presets (read-only).
- `web/packages/ui/README.md` — Plexor DS tokens, primitives.
- `../../AGENTS.md` (repo root) — the two-name system, scope
  hierarchy, migration order (background).
- `../../.agents/docs/` — Plexor-wide architecture and concepts.
- `../../.agents/rules/process/commit-format.md` — commit message
  format (`[.stbl](feat/fe/www): …`).
- `~/.agents/rules/process/agent-runtime-safety.md` — no dev servers.

## 11. Don't duplicate

This file is the **www-app-specific** layer. Project-wide rules
(commit format, no dev server, secrets, theming via Plexor DS) live
higher up — link, don't restate.
