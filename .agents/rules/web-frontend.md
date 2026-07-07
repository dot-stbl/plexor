---
description: frontend project rules for Plexor Portal (React + Vite + shadcn-ui + Base UI + Tailwind v4)
globs: ["web/**/*.ts", "web/**/*.tsx", "web/**/*.css"]
priority: high
---

# Plexor Web — frontend rules

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

## Tailwind v4

17. **Use `@theme inline` in `src/index.css`** to expose design tokens as utilities. The `inline` modifier means utilities resolve to CSS variables at runtime (so they update on theme switch).
18. **Arbitrary values are OK** when standard scale doesn't fit (`text-[11px]`, `tracking-[0.06em]`, `bg-ok/10`). Prefer the named token utilities (`bg-ok-soft`, `text-err-ink`) over arbitrary oklch.
19. **`@apply` is OK in component CSS, not in `index.css`.** Use it inside `*.tsx` files when shadcn-generated Tailwind classes need extra variants.
20. **Order matters: `@layer base` → `@layer components` → `@layer utilities`.** Tokens in `@theme inline` are read by all layers.

## Bundle

21. **Per-route code splitting** — TanStack Router does this automatically with file-based routes. Verify with `bun run build` that `/` and `/components` produce separate chunks (or at least the bundle size doesn't grow when adding new routes).
22. **No `manualChunks` unless bundle > 500 KB.** Currently 994 KB raw / ~280 KB gzip — acceptable. If grows, use `build.rollupOptions.output.manualChunks` to split vendor.
23. **Generated code is gitignored** — `src/routeTree.gen.ts` (regenerated by `@tanstack/router-cli` on build).

## Project structure

24. **`web/apps/console/`** — the single app (Plexor Portal).
25. **`src/shared/ui/primitives/`** — all shadcn-ui + custom components. One file per component. Re-exports via barrel `index.ts`.
26. **`src/lib/utils.ts`** — `cn()` helper (clsx + tailwind-merge). Nothing else.
27. **`src/routes/`** — TanStack Router file-based routes. `__root.tsx` + per-page files. `index.tsx` is `/`, `components.tsx` is `/components`.
28. **`src/index.css`** — global styles ONLY. Tokens + base layer + minimal structural utilities.
29. **No `src/styles/`, `src/css/`, `src/theme/`, etc.** All styles in `index.css`.

## When in doubt

30. **Is there a shadcn-ui primitive?** Use it. Add Plexor DS-specific variants if needed.
31. **Is there a Tailwind utility?** Use it. Add `@theme inline` token if needed.
32. **Is it a one-off static layout?** Use `.kpi` / `.audit-row` / `.toolbar*` / `.table-wrap/.tbl` / `.pagination/.pg-*` / `.field` / `.empty-state` / `.skeleton*` / `.kv-list` / `.console*` from the allowed structural list.
33. **Is it a tooltip / popover?** Use shadcn Tooltip/Popover. For pure CSS hover-only labels, use `[data-tooltip]` (already defined in `index.css`).
34. **Is it a domain-specific component (TenantCard, VMList, AuditTimeline)?** Build it in `src/features/<name>/` (Phase 1.5 — not used in MVP).
