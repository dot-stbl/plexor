# Recipe: new page

Use this when you need to add a list page, a detail page, or a create/form
page under `src/routes/`. Read `web/apps/console/AGENTS.md` first if you
haven't this session.

All commands below run from `web/apps/console`.

## 1. Pick your exemplar

| Page type | Copy this file | Notes |
|---|---|---|
| List page (simple) | `src/routes/networks.tsx` | Table + toolbar, no filters/selection |
| List page (filters + selection + empty state) | `src/routes/images.tsx` | `DataTableToolbar`, `useRowSelection`, `BulkActionToolbar` |
| Detail page | `src/routes/vms/$id.tsx` | Loading/error/loaded states, lifecycle actions, `useDocumentTitle` |
| Form / create page | none checked in yet — read `.agents/rules/web-frontend.md` rules 69-73 for the section/FieldRow/SummaryPanel layout | Ask a human if you're unsure this shape is right |

Open the exemplar. Copy its import list and structure. Don't invent a new
page shape.

## 2. Route file

- File path decides the URL: `src/routes/foo.tsx` → `/foo`;
  `src/routes/foo/$id.tsx` → `/foo/:id`.
- `export const Route = createFileRoute('/path')({ component: FooPage,
  ...routeHead('Foo') })`. Use `useDocumentTitle()` instead of `routeHead()`
  only when the title depends on loaded data (see `vms/$id.tsx`).
- List routes also set `staticData: { crumb: 'Foo' }` (see `images.tsx`) —
  the breadcrumb reads this from route matches. Don't render your own
  `<Breadcrumb>` inside the page.
- Resources at the same level are parallel routes, not nested
  (`/vms/new`, not `/clusters/$id/vms/new`).

## 3. Feature folder

Create `src/features/<name>/` with:

- `<name>-columns.tsx` — `export function getXColumns(t: TFunction):
  ColumnDef<X>[]` (a function, not a `const` — it needs `t`).
- `use-<name>.ts` — a hook returning `{ items, ...derivedCounts, isPending,
  error }`. Copy the shape of `src/features/networks/use-networks.ts`.
- `<name>-empty.tsx` — the `EmptyState` for this resource, with a CTA
  `<Button render={<Link to="/<name>/new" />}>`.
- `index.ts` — barrel: re-export every public name. The route imports only
  from this barrel, never from the internal files.

Reference: `src/features/networks/` (4 files, all four pieces above).

## 4. Data — is there an API endpoint yet?

- **Yes, it's in `contracts/plexor.openapi.yaml`** — use the kubb-generated
  hook (see `useGetVm`, `useDeleteVm` imports in `vms/$id.tsx`).
- **No** — add a handmade mock module in `src/shared/api/mocks/handmade/`.
  Copy `src/shared/api/mocks/handmade/networks.ts`: a `TODO(contract):` header
  naming the endpoint, a typed array of realistic rows, one exported list
  function. Read `src/shared/api/mocks/handmade/README.md` for the full
  convention before adding a new module.

## 5. i18n keys

- Add every label/placeholder/title/empty-state string to
  `src/shared/lib/i18n/locales/en/common.json` AND
  `src/shared/lib/i18n/locales/ru/common.json` (a test enforces parity — a
  key in only one locale fails `bun run test`).
- Don't localize enum values, units, IDs, or proper nouns (`running`,
  `GiB`, `VirtIO`) — those come from the backend as-is.
- Interpolate with `{{var}}` + `t('key', { var })`, never string
  concatenation of translated fragments.

## 6. Page story

Add `src/routes/<name>.stories.tsx` (or `<name>/$id.stories.tsx`). Copy
`src/routes/networks.stories.tsx`:

- Re-render the page body directly (don't wrap the real route — routing
  context isn't available in Storybook).
- One story per realistic state: `Default` (populated), `Empty`, and
  `Loading`/`Error` if the page has those states (see a detail-page
  equivalent for `vms/$id.tsx` if one exists, or build the loading/error
  JSX inline like the route does).
- `title: 'Pages/<Name>'` — this becomes the story id
  `pages-<name>--<story>` used by `bun run shot story`.

## 7. Shot + check

```
bun run shot routes                          # confirm your route path exists
bun run shot page /<name> --theme both       # the real route, mock API
bun run shot story pages-<name>--default --theme both
bun run shot story pages-<name>--empty --theme both
```

Read each `.md` report. Fix every issue (see `docs/agent/visual-debug.md`
for the issue-code table). Re-run until `PASS`.

Then:

```
bun run agent:check
```

Last line must read `AGENT-CHECK: PASS`. If not, read
`docs/agent/report-format.md` and fix.
