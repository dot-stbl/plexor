# web/apps/console — agent guide

Use this when you build, change, or fix a page or component in the Plexor
console. Read this file first, every session, before touching any file.
Written in simple English on purpose — you are not expected to read Russian
comments in the code (they're fine to leave as-is).

## What this is

Plexor Portal: a Vite + React 19 + TanStack Router/Query single-page app at
`web/apps/console`. It is the operator UI for the Plexor self-hosted cloud
platform. It runs against a mocked API (MSW) — you never need a live
backend to build or verify a page.

## Where things are

| Thing | Path |
|---|---|
| Routes (pages), file-based | `src/routes/` |
| Domain logic (types, hooks, columns, empty states), one folder per bounded context | `src/domains/<context>/{model,api,ui}/` — see "Domains" below |
| UI primitives (Button, Dialog, DataTable, …) | `src/shared/ui/primitives/<name>.tsx` (flat files, not folders) |
| Component decision table — check this FIRST | `src/shared/ui/INDEX.md` |
| App shell (PageTemplate, AppShell, ScopeSwitcher) | `src/shared/ui/app-shell/` |
| Data table family (DataTable, toolbar, selection) | `src/shared/ui/data-table/` |
| Mock fixtures (hand-curated data, by bounded context) | `src/mocks/<context>/` — see `src/mocks/README.md` |
| Mock API data (contract endpoints) | `src/shared/api/mocks/handlers.ts` |
| Mock data for endpoints not in the contract yet | `src/shared/api/mocks/handmade/` |
| i18n keys (EN primary, RU secondary, both required) | `src/shared/lib/i18n/locales/{en,ru}/common.json` |
| Page title helpers | `src/shared/lib/route-head.ts`, `src/shared/lib/use-document-title.ts` |
| Step-by-step recipes for this loop | `docs/agent/` |

## Domains

Domain logic lives in `src/domains/<context>/`, one folder per bounded
context (`compute`, `network`, `identity`, `catalog`, `audit`, `billing`,
`branding`, `fleet`, `dashboard`; `storage`/`scope` reserved, no FE
surface yet). Each domain has up to three subfolders:

```
src/domains/<context>/
  model/   # types narrowed from @/shared/api + pure domain logic
           #   (status→variant mappers, formatters). No JSX, no fetch.
  api/     # TanStack Query hooks wrapping kubb, or a same-shaped
           #   handmade-mock hook where the contract doesn't exist yet.
  ui/      # Context-specific components: columns, row actions, empty
           #   states, list/detail page bodies. Not routes.
  index.ts # Public barrel — the ONLY import path anything outside the
           #   domain may use (never '@/domains/<ctx>/model/*' etc.).
```

**Dependency rule, ESLint-enforced (`eslint.config.js`) + a belt-and-
suspenders script (`bun run check:domains`):**

- A route imports any number of domains' barrels (routes compose).
- A domain imports another domain ONLY via that domain's barrel, never a
  deep `model/api/ui` path.
- `src/shared/**` may **never** import a domain — not even the barrel,
  no exceptions. A shared component that needs domain data takes it as a
  **prop**; the route/composition-root fetches it (see
  `routes/__root.tsx`'s `launcherSummary` prop into `AppShell` →
  `AppSidebar` → `AppLauncher` for a worked example).
- `src/mocks/<context>/` fixtures are NOT inside `domains/` — they're a
  sibling tree so `shared/api/mocks/handlers.ts` (which needs fixture
  data to answer contract-endpoint requests) never has to import a
  domain either. A domain's `api/` hooks and `src/mocks/` fixtures may
  freely import each other's barrels/types; see
  `.agents/docs/architecture/frontend-ddd.md` §3/§7 for the full
  rationale and the migration's execution log.

Full design doc: `.agents/docs/architecture/frontend-ddd.md`.

## THE LOOP

1. Read the matching recipe in `docs/agent/` (`new-page.md`, `new-component.md`,
   or `visual-debug.md` for a bug report).
2. Open the exemplar file the recipe names. Copy its shape — imports, route
   config, folder layout.
3. Write the code.
4. Run `bun run shot page <path> --theme both` (a route) or
   `bun run shot story <story-id> --theme both` (a Storybook story).
   **Git Bash only:** prefix with `MSYS_NO_PATHCONV=1` (e.g.
   `MSYS_NO_PATHCONV=1 bun run shot page /vms --theme both`) — Git Bash
   otherwise mangles a leading `/path` argument into a Windows path. No
   prefix needed in pwsh.
5. Read the `.md` report next to the PNG. Open the PNG too if you can see
   images.
6. Fix every issue the report lists — each has a `fix:` hint. See
   `docs/agent/visual-debug.md` for the issue-code table.
7. Repeat steps 4-6 until every target prints `PASS`.
8. Run `bun run agent:check`.
9. Last line `AGENT-CHECK: PASS` → done. `AGENT-CHECK: FAIL (...)` → fix and
   go back to step 8.

Never skip step 4. A page that "looks right" in your head is not verified.

## Hard rules

1. **`src/shared/ui/INDEX.md` first.** One need → one component. If a row in
   that table fits, use it — do not invent a new primitive.
2. **shadcn/DS primitives only.** Every visible element comes from
   `src/shared/ui/primitives/` or composes them. No hand-styled raw
   `<div>`/`<button>` standing in for a real component.
3. **Icons only from `@nine-thirty-five/material-symbols-react/rounded/700`.**
   Named import (`Add`, `Delete`, `Search`, …), never from a local
   `@/shared/ui/icon` (deleted) or any other icon package.
4. **No hardcoded colors.** No `#hex`, no `oklch(...)`, no `text-blue-500`.
   Use token utilities (`bg-ok-soft`, `text-err-ink`, `border-border`, …). If
   the token you need doesn't exist, say so in your final answer — don't
   invent a raw color.
5. **`bun` only.** `bun run <script>`, `bunx --bun <tool>`. Never `npm` or
   `pnpm` inside `web/`.
6. **All user-facing text through `t('key')`.** Add the key to BOTH
   `en/common.json` and `ru/common.json` — a test fails if they're out of
   sync. Never a bare string in JSX for anything a user reads.
7. **Every route sets a title.** `...routeHead('Page name')` in the route
   config for static titles, `useDocumentTitle(name)` in the component for
   data-dependent titles. Never a route with no title.
8. **Page scaffold is `PageTemplate`** (`@/shared/ui/app-shell`). Never a
   hand-rolled header `<div>`.
9. **`DataTable` defaults to `density="compact"`.**
10. **Empty list state is `EmptyState` with a CTA button**, not a bare
    sentence. If there's a prerequisite action, the CTA navigates there.
11. **Icon-only buttons need `aria-label`.** `<Button size="icon" aria-label="...">`.
12. **No raw `<select>`, checkbox, or radio input.** Use `Select`/
    `SimpleSelect`, `Checkbox`, `RadioGroup` from primitives.
13. **Collections render as `Badge`/`StatusPill` chips**, never
    `items.join(', ')` or a count sentence.
14. **New domain logic lives in `src/domains/<context>/{model,api,ui}/`
    with an `index.ts` barrel** (see "Domains" above — pick the existing
    context it belongs to; only add a new context folder for a genuinely
    new bounded context). The route imports only from the barrel.
15. **Column defs are functions: `getXColumns(t)`**, not a module-level
    `const` (it needs `t` at call time, and `t` isn't available at module
    load).
16. **Don't edit `src/shared/ui/primitives/*` for one screen's need.** Wrap
    the primitive in a feature-local component instead.

## Never do

- Never regenerate or commit visual-regression baselines
  (`.storybook/__screenshots__/`) — those are CI-generated. `bun run shot`
  is a different, disposable tool; see `scripts/visual-tests.md`.
- Never run `taskkill`, `pkill node`, `pkill bun`, or any blanket process
  kill — it can kill your own runtime.
- Never start a dev/watch server yourself (`bun run dev`, `vite`,
  `--watch`, `storybook dev`). Use `bun run shot` to look at a page instead.
- Never call `playwright`/`puppeteer`/`chromium.launch()` directly. `bun run
  shot` is the only sanctioned way to render and look at the app.
- Never weaken or delete a test (or a rule check) to make it pass.
- Never edit `src/shared/api/src/**` — it's kubb-generated from the OpenAPI
  contract. Edit the contract, or add a handmade mock in
  `src/shared/api/mocks/handmade/`.
- Never `git push`.
- Never touch code outside `web/` (that's the C# backend) for a UI task.
- Never edit a file outside the ones you created or your recipe named —
  not even a one-line unused-var fix to silence a pre-existing
  `typecheck`/`lint` failure so `agent:check` passes. If a gate fails only
  because of code you didn't touch this session, leave that file alone and
  name the failure as pre-existing in your final answer's Open issues.

## Definition of Done

- `bun run agent:check` prints `AGENT-CHECK: PASS` as its last line — unless
  the only failure is a gate on a file you never touched this session; then
  report the real `AGENT-CHECK: FAIL (...)` line plus a note that it's
  pre-existing, instead of editing that file.
- Every route or component you touched has a story covering its realistic
  states (default / empty / loading / error, as applicable) and a clean
  `bun run shot` report (no FAIL issues).
- Any new i18n key exists in both `en/common.json` and `ru/common.json`.
- Any new primitive is added to `src/shared/ui/INDEX.md`.

## When you get stuck

If you try to fix the **same issue code on the same target** 3 times and it
still fails: stop. Do not try a 4th time. In your final answer, write:
the issue code, what you tried each time, and why you think it didn't work.
A human will take it from there.

## Recipes

- `docs/agent/new-page.md` — list page, detail page, form page
- `docs/agent/new-component.md` — new primitive in `src/shared/ui/primitives/`
- `docs/agent/visual-debug.md` — "this looks wrong" bug reports
- `docs/agent/report-format.md` — how to read a shot report / agent:check output
- `docs/agent/rules-cheatsheet.md` — condensed rules with tiny ✅/❌ examples
