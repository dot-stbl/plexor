---
description: frontend project rules for Plexor Portal (React + Vite + shadcn-ui + Base UI + Tailwind v4)
globs: ["web/**/*.ts", "web/**/*.tsx", "web/**/*.css"]
priority: high
---

# Plexor Web — frontend rules

> **Hard rule (не обсуждается): весь UI — только на shadcn-ui.** Любой видимый
> элемент строится из shadcn-примитивов в `src/shared/ui/primitives/` (адаптированных
> под Plexor DS-токены). Запрещено: сторонние UI-киты, «нативная» ручная вёрстка
> контролов/карточек в обход shadcn, параллельные flat-class системы. Нет нужного
> примитива — сначала создать его в `primitives/` по shadcn-паттерну, потом использовать.
> Подробности — правила 1–5 и 11–16 ниже.

## Architecture

0. **Package manager — `bun`, never `pnpm` или `npm`.** Web monorepo `web/` использует `bun` (см. `web/package.json` scripts: `bun --filter ...`). Не запускай `pnpm install`, `pnpm add`, `pnpm dev` или любые другие `pnpm`-команды в `web/`. Используй `bun install`, `bun add`, `bunx --bun <tool>`, `bun run <script>`. `frontend/` (deprecated, см. `.agents/process/build-verification.md` line 144) использует pnpm — это другой модуль, не путай. Распознавание: `web/` monorepo с TanStack Router — PM = `bun`; `frontend/` — PM = `pnpm`.

1. **shadcn-ui is the single source of components.** No parallel flat-class system. No custom HTML form controls styled via CSS. shadcn primitives are adapted to Plexor DS tokens, not the other way around.
2. **If shadcn-ui doesn't cover something** (FileUpload, MultiSelect, ColorPicker, custom form), create a new primitive in `src/shared/ui/primitives/` following the shadcn-ui pattern: `*-ui` package as base, `cva` for variants, `cn()` for class merging, `data-slot` for selectors, `forwardRef` if needed.
3. **Tailwind utilities are the source of truth for typography & display.** No `.eyebrow`, `.kbd`, `.code`, `.pill`, `.chip`, `.mono`, `.num`, `.tnum`, `.uppercase` flat classes. Use Tailwind: `text-[11px] uppercase tracking-[0.06em] text-muted-foreground font-medium` instead.
4. **CSS only for structural layouts shadcn-ui doesn't cover.** Allowed: `.kpi`, `.audit-row`, `.table-wrap/.tbl`, `.toolbar*`, `.pagination/.pg-*`, `.field/.field-hint`, `.empty-state`, `.skeleton*`, `.kv-list`, `.console*`, `.line-cursor`, `[data-tooltip]`. Anything else → Tailwind utility or shadcn component.
5. **No raw HTML form elements styled with custom CSS.** `<input type="text">`, `<textarea>` are OK but use the standard Tailwind className. Never custom `.input` class. `<select>`, `<input type="checkbox">`, `<input type="radio">` are NOT used directly — use shadcn Select / Checkbox / RadioGroup.

## Plexor DS tokens

6. **Tokens defined in `src/index.css` only** — `:root` (light) and `.dark` (dark). Override shadcn-ui's default tokens (--background, --primary, --ring, etc.) with Plexor DS values. Add Plexor-specific tokens (--ok/--err/--warn/--idle, --surface-2, --fs-*, --s-*, --radius-ds, --row-h, etc.).
7. **`@theme inline` maps every token to a Tailwind utility** so `bg-ok-soft`, `text-err-ink`, `border-border-2`, `font-mono`, `text-[11px]`, `bg-muted`, etc. resolve correctly. After editing tokens, verify the corresponding `@theme inline` entries exist.
8. **Theme switching via `html.dark` class.** ThemeToggle adds/removes this class. localStorage key `plexor-theme`. Initial theme applied via inline `<script>` in `main.tsx` (no flash). Never use `prefers-color-scheme` media query directly — controlled by user preference + localStorage.
9. **No hardcoded color values in components** (no `text-blue-500`, no `#hex`, no `oklch(0.5 0.1 200)` outside `index.css`). If a token doesn't exist, add it to `:root` + `@theme inline` first, then use the utility.
10. **Status semantics always use Plexor DS tokens** — `--ok` / `--ok-soft` / `--ok-ink`, `--err` / `--err-soft` / `--err-ink`, etc. No `text-red-600` / `bg-green-100` / arbitrary oklch. Use StatusPill component or build a custom primitive.

## Components

11. **shadcn-ui primitives in `src/shared/ui/primitives/`** — generated via `bunx --bun shadcn@latest add <name>`. After generation, customize for Plexor DS (add variants, change sizes, swap icon imports to `@nine-thirty-five/material-symbols-react/rounded/700`).
12. **Custom primitives same location + same pattern** — `StatusPill`, `MonoNum`, `ThemeToggle`. Use `forwardRef` when wrapping native elements that need ref. Use `cva` for variants. Use `cn()` (from `@/lib/utils`) for className merging.
13. **UI-иконки: Google Material Symbols _Rounded_ (weight 700) через `@nine-thirty-five/material-symbols-react/rounded/700`.** Tree-shakeable: каждый импорт шипит только нужную иконку (path). Импортируй компоненты напрямую из subpath (`Add`, `KeyboardArrowDown`, `Delete`, `Search`, …), НЕ из вендорного корня и НЕ из локального `@/shared/ui/icon` (такого файла больше нет). Rounded 700 = chunky default под Plexor DS soft-radius. Размер — `className="size-*"` (default size `1em` в либе, но мы почти всегда передаём явный `size-*`). **Weight prop не передавай** — стиль зашит в import path (700 = default; для rare thin-вариантов импортируй из другого subpath: `…/rounded/400`, `…/rounded/filled`). Акцент активного состояния — фоном контрола, не сменой weight в месте использования. **Filled** — опциональный для selected-состояний (`@nine-thirty-five/material-symbols-react/rounded/filled`), не для emphasis. **Manifest для агентов** (если ищешь иконку по natural language): `node_modules/@nine-thirty-five/material-symbols-react/dist/manifest.json` — у каждой иконки `tags` (синонимы).
14. **Один источник UI-иконок — `@nine-thirty-five/material-symbols-react` (subpath per style/weight).** Нет локального `@/shared/ui/icon`, нет `@phosphor-icons/react`, нет `@iconify/react` для UI-иконок, нет `unplugin-icons`, `lucide-react`, `react-icons`. Новая иконка: найди Material-имя в `manifest.json` (или [fonts.google.com/icons](https://fonts.google.com/icons?icon.set=Material+Symbols)), импортируй из `@nine-thirty-five/material-symbols-react/rounded/700`. **Без алиасов** (никакого Phosphor-нейминга): компонент в коде = Material-имя (`Add`, `Close`, `KeyboardArrowDown`, `DockToLeft`). Локальный `as`-ренейм разрешён только для разрешения коллизии имён (например `import { DockToLeft as SidebarIcon }` когда рядом локальная `Sidebar` UI-shell компонента). Цветные бренд/тех-логотипы — отдельно через `<TechIcon>` (Iconify `logos:` инлайн в `src/shared/ui/tech-icon-data.ts`, регенерируется `bun run gen:tech-icons`), см. rule 63.
15. **Button variants** (custom-adapted for Plexor DS):
    - `default` → Plexor DS primary (bg=accent monochrome dark)
    - `outline` → surface bg + border
    - `secondary`, `ghost`, `destructive`, `link` → shadcn defaults mapped to Plexor tokens
    - `danger` (Plexor DS only) → text-only, err-ink, transparent border
    - `danger-solid` (Plexor DS only) → filled red, white text
    - Sizes: `xs` (24), `sm` (28), `md` (32, default), `lg` (40), `xl` (48), `icon`/`icon-xs`/`icon-sm`/`icon-md`/`icon-lg` (square)
16. **Don't re-invent component variants with flat classes.** If a Button variant is missing, add it to the `cva` in `src/shared/ui/primitives/button.tsx`. Don't write a parallel `.btn-danger` flat class.

## Chrome spacing / visual balance

17. **Header and overlay chrome use optical-equal outer insets.** If a topbar,
    launcher, or compact control has a fixed height, choose its side padding so
    the visible top/bottom air and left/right air read equal. Avoid `h-11 px-3`
    style combinations where 5–6px vertical air fights 12px horizontal air.
    For a `48px` header with `32px` controls, use `px-2` (8px side air), not
    `px-3`.
18. **Floating close buttons inherit the surface inset.** A close `X` on a
    launcher/sheet/dialog must sit on the same inset grid as the content
    (`top-3.5 right-3.5` when the panel uses `p-3.5`). If a scrollbar would sit
    under or too close to the close button, use the shadcn/Base UI
    `<ScrollArea>` primitive and inset the rail instead of relying on native
    scrollbar placement.

## Tables — compact and copyable

19. **Data tables default to `density="compact"`.** Tight rows (`h-8` header,
    `h-7` cell, `text-[11px]` body). 8+ rows fit comfortably above the fold.
    Only use `density="comfortable"` for very long content (settings, audit log).
20. **Copyable values use `<CopyableText value="...">`.** IDs, IPs, hostnames,
    cluster names — any value a sysadmin will re-type or paste into a terminal —
    wrap in `<CopyableText>`. The icon button is muted at rest, reveals its
    background on `hover` (using the wrapper's `group/copy` selector), shows a
    check tick on success, and toasts a confirmation via `sonner`. Never render
    a copy icon as a raw `<button>` with a manual `navigator.clipboard.writeText`
    call — funnel it through the primitive so the affordance is consistent.
21. **Empty states have a CTA, not just a sentence.** A page that needs the
    user to take an action before the primary task is possible (e.g. "install
    Plexor before you can create a VM", "add a node before you can create a
    VM") renders the `<Empty>` primitive with a `<Button>` that navigates to
    the prerequisite screen. A disabled form with no path forward is a bug.


## IconButton + visual hierarchy + action density

**Not every action is a button. Don't flatten visual hierarchy.**

**Not every action is a button. Don't flatten visual hierarchy.**

UI has **5 levels of emphasis**. Each level has a specific role; using the wrong level kills the hierarchy.

| Level | When | Component | Examples |
|---|---|---|---|
| **1. Primary** | ONE per view, the main CTA. Black/white button. | `<Button>` variant="default" | Create VM, Save changes, Submit |
| **2. Secondary** | Other actions in the view. Outlined or text-only. | `<Button>` variant="outline" / "ghost" | Cancel, Reset, Filter |
| **3. Destructive** | Reversible-with-cost or destructive actions. | `<Button>` variant="destructive" | Delete, Terminate, Force-stop |
| **4. Tertiary** | Inline links inside paragraphs, table cells, lists. | `<a>` / shadcn `Link` styled | "View details", "Edit", row action labels |
| **5. Icon-only** | Compact actions in toolbars, table rows. | `<Button size="icon">` | Settings cog, close X, refresh |

### Rules

- **Max ONE `default` button per view** — the primary action. All other actions use `outline`, `ghost`, or `destructive`.
- **Icon-only buttons use `size="icon"` / `size="icon-sm"`** — never `<Button>⚙</Button>` with text-glyph. Always SVG-иконка из `@nine-thirty-five/material-symbols-react/rounded/700`: `<Button size="icon" aria-label="settings"><Settings /></Button>`.
- **Icon + text in a button** — when the icon helps recognition: `<Button><Pause /> Suspend</Button>`. Icon goes FIRST, then text. Never text + icon in this order (looks weird in RTL or when text is wrapped).
- **SVG-иконки auto-size** via shadcn's `[&_svg:not([class*='size-'])]:size-4` selector as defense-in-depth, but **always pass explicit `className="size-X"`** on icons. Default `size-4` (16px). Use `size-3.5` (14px) inside size=sm button, `size-3` (12px) inside icon-sm, `size-3.5` inside default. Relying on parent selector alone is fragile.
- **Inline row actions** (table rows) — use `Button size="icon-sm" variant="ghost"` per action. Up to 3-4 per row; if more, use shadcn `DropdownMenu` triggered by `size="icon"` with `<DotsThree />`.
- **Tabular action density** — actions that appear in EVERY row (delete, edit, view) belong to a consistent action group at the row's right edge. Not as primary buttons.
- **Status actions color-code by semantic, not visual** — use variant="destructive" for delete, NEVER `bg-red-500` arbitrary. Variant uses Plexor DS tokens automatically.

### Text alignment — kill the drift

Flex containers with text + icon + button often drift vertically. Common causes and fixes:

```tsx
// ❌ Wrong — emoji icons have inconsistent baseline (warn: text drifts)
// ✅ Fix — SVG icons from @nine-thirty-five/material-symbols-react/rounded/700, no className size (auto-sized by Button)
<Button size="icon" aria-label="settings"><Gear /></Button>

// ❌ Wrong — MonoNum digit baseline drifts from surrounding text
// (font-mono has different cap height than font-sans)
// ✅ Fix — inline-block align-middle leading-none on MonoNum primitive
<span className="inline-block align-middle font-mono tabular-nums leading-none">42</span>

// ❌ Wrong — vertical divider in flex bar doesn't stretch, looks off
<span className="h-5 w-px bg-border" />
// ✅ Fix — use self-stretch so the divider fills parent height
<span className="self-stretch w-px bg-border" />

// ❌ Wrong — text-only button with no min-height drifts when wrapping
<Button className="text-xs">Cancel</Button>
// ✅ Fix — use a size variant, never override height
<Button size="sm" variant="ghost">Cancel</Button>
```

**Rule of thumb:** when mixing fonts (mono + sans), icons + text, or buttons + dividers in a flex container:
1. Add `items-center` to the parent (vertical centering)
2. Use `self-stretch` on fixed-height dividers
3. Use `inline-block align-middle` on text-only inline elements that don't behave like text (MonoNum, code)
4. Never use emoji as UI icons — always SVG icons from `@nine-thirty-five/material-symbols-react/rounded/700`

## Select / Combobox / Popover — overlay pitfalls

Base UI's overlay components (`Select.Popup`, `Combobox.Popup`, `Popover.Popup`) come with default styles that create **phantom padding** you won't notice until devtools:

**1. `alignItemWithTrigger=true` (default for Select) adds +28px**

Default behavior of `Select.Positioner` is `alignItemWithTrigger=true` (when not touch input). This makes the popup *visually overlap* the trigger so the selected item's text aligns with the trigger's value text. Side effect in CSS:

```css
&[data-side='none'] {
  min-width: calc(var(--anchor-width) + 1.75rem);  /* +28px */
}
```

**Visible bug:** popup is 28px wider than trigger; items have phantom trailing right padding.

**Fix:** always `alignItemWithTrigger={false}` on `Select.Content`:

```tsx
<SelectPrimitive.Popup
  className="min-w-(--anchor-width) origin-(--transform-origin) ..."
  ...
/>
```

<Select.Positioner alignItemWithTrigger={false}>

  <Select.Popup className="min-w-(--anchor-width) ..." />
</Select.Positioner>

**2. Global `scrollbar-gutter: stable` reserves 10px on every scrollable element**

If you globally set `* { scrollbar-gutter: stable }` (good for pages to prevent layout shift on scrollbar appearance), every popup list inside has 10px phantom right padding, even when no scrollbar is visible.

**Fix:** scope `scrollbar-gutter: auto` to popup descendants:

```css
[data-slot="select-content"] *,
[data-slot="combobox-content"] * {
  scrollbar-gutter: auto;
}
```

**3. Check icon must be on RIGHT (Base UI / shadcn convention)**

Do NOT use `Select.ItemIndicator` with `absolute right-2` wrapping — that's a shadcn-implementation smell. Use the canonical grid pattern:

```tsx
<SelectPrimitive.Item
  className="grid grid-cols-[0.875rem_1fr] items-center gap-2 rounded-sm py-1.5 pr-2 pl-2"
>
  <SelectPrimitive.ItemText className="col-start-2">{children}</SelectPrimitive.ItemText>
  <SelectPrimitive.ItemIndicator className="col-start-1 flex items-center justify-center">
    <Check className="size-3.5 text-foreground group-data-[highlighted]/select-item:text-accent-foreground" />
  </SelectPrimitive.ItemIndicator>
</SelectPrimitive.Item>
```

Or flex variant (simpler):
```tsx
className="flex items-center gap-2 ..."
```
- `ItemText` with `flex-1` (text fills available)
- `ItemIndicator` with `ml-auto` (check pushed right)

**4. Hover color flip on Check**

When item is highlighted (hover/focus), background → `bg-accent` (dark). Check must flip to `text-accent-foreground` (light). Use the group pattern:

```tsx
<Select.Item className="group/select-item ..."
  data-highlighted:bg-accent data-highlighted:text-accent-foreground>
  <Check className="text-foreground group-data-[highlighted]/select-item:text-accent-foreground" />
</Select.Item>
```

**5. Иконки — из `@nine-thirty-five/material-symbols-react/rounded/700`, не из вендора**

```ts
// ✅ Works — Material Symbols Rounded под Phosphor-совместимыми именами:
import { Check, KeyboardArrowDown } from '@nine-thirty-five/material-symbols-react/rounded/700';

// ❌ Пакет удалён:
import { Check, CaretDown } from '@phosphor-icons/react';
```

The `Icon` suffix is hugeicons naming. shadcn-CLI regenerate produces wrong imports — always sed-rename to no-suffix form.

**6. Select.Trigger on `<button>` element quirks**

- `<button>` has browser default styles. Adding `inline-flex` may not always beat the user-agent stylesheet.
- `line-clamp-1` on `SelectValue` adds `display: -webkit-box` which forces min-width on the value. Use `truncate` instead — it's the Tailwind v4 way and doesn't force display changes.
- If trigger content-sized with `w-fit` looks too wide, check for `data-slot=select-trigger` parent selectors (`[&>[data-slot=select-trigger]:not([class*='w-'])]:w-fit` may override your class).

**7. Show aria-label on icon-only BulkActionToolbar buttons**

```tsx
<Button size="icon-sm" variant="ghost" aria-label="Clear selection">
  <X className="size-3.5" />
</Button>
```

Required for screen readers to announce purpose.

## Tailwind v4

19. **Use `@theme inline` in `src/index.css`** to expose design tokens as utilities. The `inline` modifier means utilities resolve to CSS variables at runtime (so they update on theme switch).
20. **Arbitrary values are OK** when standard scale doesn't fit (`text-[11px]`, `tracking-[0.06em]`, `bg-ok/10`). Prefer the named token utilities (`bg-ok-soft`, `text-err-ink`) over arbitrary oklch.
21. **`@apply` is OK in component CSS, not in `index.css`.** Use it inside `*.tsx` files when shadcn-generated Tailwind classes need extra variants.
22. **Order matters: `@layer base` → `@layer components` → `@layer utilities`.** Tokens in `@theme inline` are read by all layers.

## Bundle

23. **Per-route code splitting** — TanStack Router does this automatically with file-based routes. Verify with `bun run build` that `/` and `/components` produce separate chunks (or at least the bundle size doesn't grow when adding new routes).
24. **No `manualChunks` unless bundle > 500 KB.** Currently 994 KB raw / ~280 KB gzip — acceptable. If grows, use `build.rollupOptions.output.manualChunks` to split vendor.
25. **Generated code is gitignored** — `src/routeTree.gen.ts` (regenerated by `@tanstack/router-cli` on build).

## Project structure

26. **`web/apps/console/`** — the single app (Plexor Portal).
27. **`src/shared/ui/primitives/`** — all shadcn-ui + custom components. One file per component. Re-exports via barrel `index.ts`.
28. **`src/lib/utils.ts`** — `cn()` helper (clsx + tailwind-merge). Nothing else.
29. **`src/routes/`** — TanStack Router file-based routes. `__root.tsx` + per-page files. `index.tsx` is `/`, `components.tsx` is `/components`.
30. **`src/index.css`** — global styles ONLY. Tokens + base layer + minimal structural utilities.
31. **No `src/styles/`, `src/css/`, `src/theme/`, etc.** All styles in `index.css`.
32. **`src/features/<name>/`** — per-screen business logic. Flat siblings (no nested dirs while the screen is the only consumer). Public surface via `index.ts` barrel. Pure helpers and presentational components live side-by-side. When a 2nd screen needs the same helpers, split into `_shared/`.

## Composition over custom markup

33. **shadcn primitives compose, never re-invent.** A table is `<Table>` + `<TableHeader>` + `<TableRow>` + `<TableCell>`. A card is `<Card>` + `<CardHeader>` + `<CardContent>`. A dialog is `<Dialog>` + `<DialogContent>` + `<DialogHeader>` + `<DialogFooter>`. Don't re-style HTML elements when a primitive exists.
34. **Custom markup only when no primitive covers it.** Allowed structural classes (`.kpi`, `.audit-row`, `.toolbar*`, `.table-wrap/.tbl`, `.pagination/.pg-*`, `.field`, `.empty-state`, `.skeleton*`, `.kv-list`, `.console*`) live in `index.css` for one-off layouts that shadcn can't express. Everything else → shadcn primitive or Tailwind utility.
35. **Wrap, don't fork.** When a shadcn primitive needs project-specific tweaks (different size, extra padding, role-specific colors), wrap it in a thin local component (`VmFiltersBar` wraps `Select` + `Input`). Don't edit `src/shared/ui/primitives/*` for one-screen needs.
36. **Collections render as Badge primitives, not as text.** Never render a list of items as a comma-separated string (`items.join(', ')`) or as a plain count sentence (`{n} провайдеров`). Each item gets its own `<Badge>` (or `<StatusPill>` for statuses); overflow collapses into a single `<Badge>+N</Badge>`. The count lives on the container (`flex items-center gap-1` over the badges), never as a sentence before the list.

## Helper extraction (pure functions live in features/)

37. **Extract a pure helper when** (a) the same branching/lookup appears in 2+ places, OR (b) the logic is independently testable, OR (c) the page JSX becomes unreadable because of inline ternaries. Never extract prematurely — a one-line `switch` inline is fine.
38. **Pure helpers live in `src/features/<name>/*.ts`** (not `.tsx` if there's no JSX). Exhaustive switches on closed enums (like `VmStatus`) keep the contract safe — adding a value to the API forces a compile error here until mapped.
39. **Barrel re-exports the feature surface.** `features/vms/index.ts` lists every public name (components + helpers + types). Screens import from `@/features/vms`, not from internal files. Internal helpers stay unexported until a second consumer needs them.

## Forms — FieldGroup + Field + FieldLabel

40. **Form layout is `FieldGroup` + `Field` + `FieldLabel`.** Never use raw `<div className="space-y-4">` or `<div className="grid gap-4">` for form layout — the Field primitive handles vertical/horizontal/responsive orientations, invalid state, label-to-control association.
41. **Field anatomy:**

   ```tsx
   <Field data-invalid={hasError}>
     <FieldLabel htmlFor="email">Email</FieldLabel>
     <Input id="email" aria-invalid={hasError} />
     <FieldDescription>Used for sign-in and alerts.</FieldDescription>
   </Field>
   ```
   `data-invalid` on `<Field>`, `aria-invalid` on the actual control. Disabled → `data-disabled` on `<Field>`, `disabled` on the control.
42. **No nested interactive elements.** Don't put a `<Button>` inside a `<FieldLabel>` — the `has-[>[data-slot=field]]` selector breaks. Place the action next to the label or above the field.
43. **Submit button is part of the page footer, not the last Field.** The Dialog's `DialogFooter` or the form's submit row lives outside the FieldGroup.

## Reusable component design (cva + render + polymorphic)

43. **Visual variants via `cva`, not prop sprawl.** A `<Button variant="outline" size="sm">` beats `<Button isOutline isSmall>`. See `src/shared/ui/primitives/button.tsx` for the canonical pattern.
44. **`render` prop for polymorphic triggers.** shadcn primitives that wrap `<button>` (DropdownMenuTrigger, DialogTrigger, SidebarTrigger) take `render={<Link to="..." />}`. Use this for any non-button trigger — don't reach for `asChild` (it's radix-legacy; base-ui uses `render`).
45. **Custom primitive goes in `src/shared/ui/primitives/`** when **all three** are true: (a) used by 2+ features, (b) carries its own semantics (not just styling), (c) passes the shadcn rules (one file, no horizontal padding hacks, no `!important`). If only one feature needs it → keep in that feature folder.
46. **Icon-only controls need `aria-label`.** `<Button size="icon-sm" aria-label="Действия"><MoreHoriz /></Button>` — never bare icon. Иконки импортируются из `@nine-thirty-five/material-symbols-react/rounded/700` (`MoreHoriz` и т.п.).

## State management

53. **Bytes go through `<Size bytes={...}>`, never ad-hoc spans.** Plexor's API returns raw bytes (RAM, disk, image size). The `<Size>` primitive picks the right unit (KiB / MiB / GiB / TiB) and renders the value + unit. For legacy mock data in GiB, use `SizeUtils.gibToBytes(n)` to convert. Never compose `<MonoNum>{x}</MonoNum><span>GB</span>` — the unit is stuck at GB, no auto-scaling, no binary/decimal switch. The value is rendered in a tabular font; the unit suffix is slightly smaller and muted, matching the MonoNum / StatusPill visual rhythm.

## State management

47. **Derived data with `useMemo` only when it costs something.** `filterVms(items, filters)` over 8 items doesn't need memo. A 1000-row filter or a `Set` construction (for O(1) lookup in `every`/`some` over a long array) does.
48. **Selection state lives in a `Set<string>`**, not an array. `selectedIds.has(id)` is O(1). Build it from `new Set(arr)` — never mutate, always return a new instance in setState.
49. **Toast for action feedback, not as a primary channel.** Toasts are fire-and-forget; if the user needs the result visible (e.g. "delete" with an undo), use inline state in the row, not a toast.
50. **Debounce in the parent, not the child.** `<VmFiltersBar onChange />` fires synchronously; the page wraps `setFilters` with a debounced setter if needed. Keeps the child component pure and reusable.
51. **Resources at the same level live as parallel routes, not nested ones.** VMs and Clusters are first-class resources that the user reaches independently. Creating a VM does NOT route through `/clusters/$id/vms/new` — it lives at `/vms/new` and references a Cluster via an inline `<Select>` (or whichever FK the resource has). A nested route (`/clusters/$id/vms/new`) is wrong because it forces the user through a separate resource to act on a different one. Reserve nested routes for genuine parent/child actions on the SAME resource (e.g. `/clusters/$id/nodes/new`).
52. **Persisted UI state goes through a library hook, not raw `localStorage` / cookie access.** Use `useLocalStorage` from `@uidotdev/usehooks` (already in the project) for any value that should survive reloads — sidebar expanded/collapsed, density toggle, recent items, last-selected cluster, etc. The shadcn `SidebarProvider` ships with cookie-based persistence; for our SPA the hook + `localStorage` is the cleaner choice (no cookie domain/path attributes, no SSR mismatch). The hook returns `[value, setValue]` like `useState` — swap it in, no API change.

## When in doubt

52. **Is there a shadcn-ui primitive?** Use it. Add Plexor DS-specific variants if needed.
53. **Is there a Tailwind utility?** Use it. Add `@theme inline` token if needed.
54. **Is it a one-off static layout?** Use `.kpi` / `.audit-row` / `.toolbar*` / `.table-wrap/.tbl` / `.pagination/.pg-*` / `.field` / `.empty-state` / `.skeleton*` / `.kv-list` / `.console*` from the allowed structural list.
55. **Is it a tooltip / popover?** Use shadcn Tooltip/Popover. For pure CSS hover-only labels, use `[data-tooltip]` (already defined in `index.css`).
56. **Is it a domain-specific component (TenantCard, VMList, AuditTimeline)?** Build it in `src/features/<name>/`. Pure helpers next to the components, barrel-exported.

## Interaction patterns (YC-эталон, монохром)

> Каталог паттернов и разбор референсов — `.agents/docs/ui/patterns.md`. Ниже —
> обязательные правила. YC = эталон структуры; **цвет НЕ копируем** (у нас ink).

57. **Крошки — только в верхнем баре, из route-matches.** `AppHeader` строит
    breadcrumb из `staticData.crumb` matched-роутов. **Никогда** не рендерь
    `<Breadcrumb>` внутри страницы — это даёт дубль. Каждый роут декларирует свой
    `staticData: { crumb }`.
58. **Чром страниц — через layout-routes + `<Outlet/>`, не per-page.** Общий
    каркас (заголовок, actions, контент-область) живёт в layout-роуте
    (`routes/<resource>/route.tsx`), страницы открываются в `<Outlet/>`. Текущий
    per-page `PageHeader` — **deprecated**; новые экраны используют шаблоны
    (`ListTemplate` / `DetailTemplate` / `CreateTemplate`).
59. **Create — секционированная одностраничная форма, НЕ модалка** (и не wizard,
    если это не настоящий multi-step). Секции с жирными заголовками; поля —
    **горизонтальные** (`FieldRow`: label + `?` help + `*` required слева, контрол
    справа). Футер: `Создать` (primary) · `Отменить` · `</> Генерация кода`.
60. **Выбор из вариантов — тремя контролами, selected = ink (не синий):**
    `SegmentedControl` (2-4 взаимоисключающих), `SelectableCardGrid` (пресеты/тиры,
    большие option-карты), single-select строки таблицы (длинные списки, + «Показать
    все N»). Не плоские кастом-кнопки.
61. **Каждый нетривиальный create даёт «Генерация кода»** — модалка с табами
    Terraform / `plx` CLI, `CodeBlock` (номера строк + copy). Это power-фича из
    вижна (IaC / persona Vasya), не опция.
62. **Inline-callout (`Alert`) для последствий** — info (нейтральный) / warning
    (`warn`) прямо в форме, где настройка имеет последствия. Только семантика, не
    украшение.
63. **Тех/сервис-логотипы — ЦВЕТНЫЕ через `<TechIcon slug=…>`** (Iconify `logos:`,
    данные инлайн в `tech-icon-data.ts` через кодоген `gen:tech-icons`, рендер `@iconify/react`).
    Осознанный карв-аут: UI-чром строго
    монохром, но ЛОГОТИПЫ продуктов — в бренд-цвете (узнаваемость: launcher, каталог,
    empty, sidebar managed). Нет цветного лого (`clickhouse`/`garnet`/`minio`/`ceph`) →
    Material-generic fallback. НЕ Simple Icons (удалён).
64. **Field-label: `?` HelpTooltip + красная `*` required** там, где уместно
    (config-формы). Помогает без загромождения.

## Локализация (i18n) — react-i18next

65. **Весь user-facing текст — через `t('...')`** (`useTranslation`). Ключи — в
    `src/shared/lib/i18n/locales/{en,ru}/common.json` (вложенные). EN — первичный
    язык, RU — вторичный. Хардкод-строк, которые видит пользователь, быть не должно:
    заголовки, лейблы, описания, `help`, плейсхолдеры, тосты, `staticData.crumb`,
    empty-states, bulk-действия, «N selected».
66. **Технические/бэкенд-значения НЕ локализуем** — приходят с бэка, одинаковы в
    любом языке: enum-статусы (`running`/`stopped`/`ready`), единицы
    (`GiB`/`MiB`/`vCPU`/`MB/s`), proper nouns (`VirtIO`, `q35`, `UEFI`, `K3s`,
    `Ceph RBD`, `PostgreSQL`), провайдеры, ID/hostname/CIDR/endpoint, значения
    `filter.options`. Локализуем ТОЛЬКО натуральный текст (лейбл/плейсхолдер/help/заголовок).
67. **Заголовки/фильтры колонок — через функцию, не `const`.** Колонки =
    `export function getXColumns(t: TFunction): ColumnDef<T>[]` (module-level `const`
    не может звать `t`). `header`/`filter.placeholder`/`copyLabel` → `t('table.*')`
    (общий namespace; повторяющиеся заголовки Name/Status/Created переиспользуют ключ).
    Консьюмер: `const columns = useMemo(() => getXColumns(t), [t])`.
68. **Интерполяция — `{{var}}`** + `t('key', { var })`; без конкатенации переведённых
    фрагментов (строки с разметкой → раздельные ключи `xBefore`/`xAfter`, `Trans` в
    проекте не используется). Каждый `t('literal')` обязан иметь ключ в `en/common.json`
    (иначе рендерится сырой ключ) — проверяй перед сдачей.

## Формы (create) — каркас и поля

69. **Каркас create-формы:** `PageTemplate width="full"` → грид
    `lg:grid-cols-[minmax(0,1fr)_340px]`: слева карточки-секции (`Card`), справа
    **липкая `SummaryPanel`** «что развернётся». Карточки идиоматично (`<Card>` +
    `<CardHeader className="border-b border-border">` + `<CardContent>`), БЕЗ
    костыля `gap-0 p-0` и ручных `p-4` (компонент сам держит отступы).
70. **Поля — `FieldRow`** (горизонтально: label + `?`help + `*`required слева, контрол
    справа). **Без разделительных линий** между полями и **без `py` на строке** —
    равный вертикальный ритм задаёт ОДИН `gap` на контейнере:
    `<CardContent className="flex flex-col gap-2">`. Правишь ритм формы одним `gap-2`.
71. **«Advanced»-knob'ы — `<Disclosure variant="card">`** (поверх shadcn `Collapsible`):
    разворачиваемая карточка (рамка + шапка-триггер, карет справа). Базовые поля видны
    всегда, глубина — по клику. `variant="inline"` — лёгкий текст-триггер, когда рамка
    избыточна. **НЕ `Accordion`** (фикс-высота/группа), **НЕ сырой div/button**.
72. **Инпуты — наши примитивы поверх shadcn:** `SizeField` (RAM/диск — отдаёт БАЙТЫ,
    точность до МиБ, единица-селект), `Stepper` (счётчики; кламп на blur; нативные
    спиннеры скрыты `[appearance:textfield]`), `SimpleSelect` (строковые опции),
    `SegmentedControl` (2-4 варианта, selected=ink), `PasswordInput`, `RepeatableRows`
    (key=value / списки). Размеры ВСЕГДА байты → `<Size bytes>` / `SizeUtils.format`
    (тосты/aria). Никогда `<MonoNum>{x} GiB</MonoNum>` — см. rule 53.
73. **Self-hosted-глубина (эталон Proxmox, ≠ managed):** выставляй knob'ы, которые
    managed-облако прячет (placement/нода, гипервизор machine/firmware/CPU-type,
    storage пул/шина/кэш, сеть NIC/VLAN/IP/firewall, cloud-init, guest-agent). Опции
    **адаптируются под `node.spec.providers`** (пулы/fabric/рантайм) — нельзя предложить
    бэкенд, которого нет на выбранной ноде.

## Таблицы, списки, ресурсы

74. **Table-family компонуется** (не переизобретается): `DataTable` (`density="compact"`)
    + `DataTableToolbar` (фильтры из `meta.filter` колонок + шестерёнка настройки колонок
    в одном баре, `justify-between`, reset = icon-button) + `useRowSelection` →
    `BulkActionToolbar` (чекбоксы + bulk-действия). Фильтрация: сервер →
    `compactFilters(filters)` в API; локальные данные → `applyFilters(rows, filters, columns)`.
    Новый фильтр = одна декларация `meta.filter` на колонке.
75. **IA ресурса: список — вход, создание — из шапки списка.** Каждый разворачиваемый
    ресурс = layout-роут + `index` (СПИСОК, full-width) + `new` (+ `$id` деталь). Nav/
    лаунчер ведут на СПИСОК (`/vms`, `/lxc`, `/k8s`, `/images`), НИКОГДА на `/new`.
    «Создать *» — CTA в шапке (`PageTemplate actions`) → `/new`. Пустой список — `EmptyState`.
76. **`EmptyState`** — онбординг пустого списка/раздела: иллюстрация/иконка слева, справа
    заголовок + текст + doc-ссылки + CTA. Это НЕ shadcn-дефолт `Empty` (тот центрированный,
    для «ничего не найдено» после фильтра).

## Gotchas

77. **`scrollbar-gutter: stable` — только на `html`, НЕ на `*`.** На `*` любой
    `overflow-hidden` элемент (badge/card/popup) получает фантомный правый отступ под
    скроллбар.
78. **`Badge`/`StatusPill` — `leading-none`** при фиксированной высоте (иначе текст не по
    центру). `Badge` — только короткие токены, не широкий/смешанный контент (для этого —
    muted-текст).
