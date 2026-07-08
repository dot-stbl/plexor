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

11. **shadcn-ui primitives in `src/shared/ui/primitives/`** — generated via `bunx --bun shadcn@latest add <name>`. After generation, customize for Plexor DS (add variants, change sizes, swap icon library to Phosphor).
12. **Custom primitives same location + same pattern** — `StatusPill`, `MonoNum`, `ThemeToggle`. Use `forwardRef` when wrapping native elements that need ref. Use `cva` for variants. Use `cn()` (from `@/lib/utils`) for className merging.
13. **Icons: `@phosphor-icons/react` only.** v2.1.x exports icons WITHOUT `Icon` suffix (`X`, `CaretDown`, `Check` — not `XIcon`, `CaretDownIcon`, `CheckIcon`). All shadcn-generated `<icon>Icon` imports must be sed-renamed on add.
14. **No `lucide-react`, no `react-icons`.** Phosphor is the single icon library.
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
- **Icon-only buttons use `size="icon"` / `size="icon-sm"`** — never `<Button>⚙</Button>` with text-glyph. Always Phosphor SVG: `<Button size="icon" aria-label="settings"><Gear /></Button>`.
- **Icon + text in a button** — when the icon helps recognition: `<Button><Pause /> Suspend</Button>`. Icon goes FIRST, then text. Never text + icon in this order (looks weird in RTL or when text is wrapped).
- **Phosphor icons auto-size** via shadcn's `[&_svg:not([class*='size-'])]:size-4` selector as defense-in-depth, but **always pass explicit `className="size-X"`** on icons. Default `size-4` (16px). Use `size-3.5` (14px) inside size=sm button, `size-3` (12px) inside icon-sm, `size-3.5` inside default. Relying on parent selector alone is fragile.
- **Inline row actions** (table rows) — use `Button size="icon-sm" variant="ghost"` per action. Up to 3-4 per row; if more, use shadcn `DropdownMenu` triggered by `size="icon"` with `<DotsThree />`.
- **Tabular action density** — actions that appear in EVERY row (delete, edit, view) belong to a consistent action group at the row's right edge. Not as primary buttons.
- **Status actions color-code by semantic, not visual** — use variant="destructive" for delete, NEVER `bg-red-500` arbitrary. Variant uses Plexor DS tokens automatically.

### Text alignment — kill the drift

Flex containers with text + icon + button often drift vertically. Common causes and fixes:

```tsx
// ❌ Wrong — emoji icons have inconsistent baseline (warn: text drifts)
// ✅ Fix — Phosphor SVG icons, no className size (auto-sized by Button)
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
4. Never use emoji as UI icons — always Phosphor SVGs

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

**5. Phosphor 2.1.x exports have NO `Icon` suffix**

```ts
// ✅ Works:
import { Check, CaretDown } from '@phosphor-icons/react';

// ❌ Doesn't exist (would silently import nothing):
import { CheckIcon, CaretDownIcon } from '@phosphor-icons/react';
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

## When in doubt

32. **Is there a shadcn-ui primitive?** Use it. Add Plexor DS-specific variants if needed.
33. **Is there a Tailwind utility?** Use it. Add `@theme inline` token if needed.
34. **Is it a one-off static layout?** Use `.kpi` / `.audit-row` / `.toolbar*` / `.table-wrap/.tbl` / `.pagination/.pg-*` / `.field` / `.empty-state` / `.skeleton*` / `.kv-list` / `.console*` from the allowed structural list.
35. **Is it a tooltip / popover?** Use shadcn Tooltip/Popover. For pure CSS hover-only labels, use `[data-tooltip]` (already defined in `index.css`).
36. **Is it a domain-specific component (TenantCard, VMList, AuditTimeline)?** Build it in `src/features/<name>/` (Phase 1.5 — not used in MVP).
