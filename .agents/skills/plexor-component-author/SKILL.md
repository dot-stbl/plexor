---
name: plexor-component-author
description: Build new UI components for the Plexor Portal monorepo. Use when the user asks to create a new component, when an existing shadcn-ui primitive doesn't fit the use case, or when a custom abstract primitive is needed (e.g. bulk action bar, status pill, metric card). Applies to anything in `web/apps/console/src/shared/ui/primitives/`. Triggers on "создай компонент", "new component", "add component", "bulk action", "toolbar", "metric card", etc.
user-invocable: false
allowed-tools: Bash(bunx --bun shadcn@latest *), Bash(bunx --bun tsr generate)
---

# Plexor DS — Component Author

Build new UI components for the Plexor Portal (`web/apps/console/src/shared/ui/primitives/`).

> **Rule (read first):** before creating a new component, check if a shadcn-ui primitive already covers the use case. Run `bunx --bun shadcn@latest search @shadcn -q "<your-need>"` or look at `web/apps/console/src/shared/ui/primitives/`. Only create a custom primitive when shadcn doesn't cover it.

> **Naming:** Plexor DS custom components use `PascalCase.tsx`. The file name = the export name (`StatusPill.tsx` exports `StatusPill`). Group in the same `primitives/` folder.

> **No custom CSS classes for layout or styling.** Tailwind utilities + Plexor DS tokens only. The CSS file (`index.css`) is reserved for:
> 1. Token definitions in `:root` and `.dark`
> 2. `@theme inline` mapping tokens to Tailwind utilities
> 3. Minimal `@layer base` resets + browser-level concerns (scrollbar, smooth scroll, focus ring)
> Never write `@layer utilities { .my-class { ... } }`. If you can't express something in Tailwind, the design system needs a new token — add it to `@theme inline` first.

## When to Create a Custom Component

Create a new component in `src/shared/ui/primitives/` ONLY when:

1. **The pattern is abstract** — used in 2+ unrelated places (e.g. `<Stat>` for billing cards AND dashboard cards AND page headers).
2. **shadcn-ui doesn't cover it** — shadcn Base UI has 60+ primitives. If you need something specific (e.g. "compact status indicator with solid dot + soft background"), the registry won't have it.
3. **You'd repeat the same JSX** in 3+ places without abstracting.

Do NOT create a component for:

- A single-use layout block — use Tailwind utilities + shadcn composition directly.
- A token override (use existing `bg-*` utilities).
- A simple styled element — use `<div className="...">` directly.
- Static visual content — put it in a page component.

Examples of legitimate custom primitives in this project:

| Component | Why it's a primitive | Why it's not a shadcn thing |
|---|---|---|
| `StatusPill` | 3-token status indicator with solid dot | shadcn Badge is generic, no status semantics |
| `MonoNum` | `font-mono` + `tabular-nums` + optional muted | Trivial enough to be its own component for consistency |
| `Stat` | Single-metric card with trend indicator | shadcn Card is generic; needs custom layout |
| `Console` | Terminal / serial-log panel with blinking cursor | shadcn has no terminal component |
| `Toolbar` (planned) | Filter bar with search + filter chips + actions | shadcn has no Toolbar primitive in the Base UI registry |

## File Structure

```
src/shared/ui/primitives/
  ├── <Name>.tsx          ← component implementation
  └── index.ts            ← barrel export (created by `bunx tsr generate` or hand-written)
```

A typical component file:

```tsx
import { cn } from '@/lib/utils';
import { Button } from '@/shared/ui/primitives/button';
import type { ComponentProps } from 'react';

/**
 * StatusPill — compact status indicator (dot + label).
 *
 * Abstract: any "one of N statuses" use case (running/idle/error/pending).
 * Uses Plexor DS status semantics tokens (bg-ok-soft, text-ok-ink, etc.).
 */
export type StatusVariant = 'ok' | 'err' | 'warn' | 'idle' | 'running' | 'failed' | 'pending' | 'stopped';

export interface StatusPillProps extends ComponentProps<'span'> {
  variant: StatusVariant;
  hideDot?: boolean;
}

export function StatusPill({ variant, hideDot = false, className, children, ...props }: StatusPillProps) {
  return (
    <span
      data-slot="status-pill"
      data-variant={variant}
      className={cn(
        'inline-flex items-center gap-1.5 rounded-full px-2 py-0.5 text-xs font-medium whitespace-nowrap',
        `bg-${variant === 'running' || variant === 'ok' ? 'ok' : variant === 'err' || variant === 'failed' ? 'err' : variant === 'warn' || variant === 'pending' ? 'warn' : 'idle'}-soft`,
        // ... more specific
        className,
      )}
      {...props}
    >
      ...
    </span>
  );
}
```

### Required conventions

- **`data-slot="<name>"`** on the root element — used by `data-slot~='...'` selectors elsewhere (e.g. scrollbar containment on `[data-slot~='dialog-content']`).
- **`cn()` for conditional classes** — never template-literal ternaries.
- **No `<style>` or `@apply` blocks** in component files. Pure Tailwind utilities.
- **Props from `ComponentProps<'element'>`** — extend the base HTML element props.
- **Use Plexor DS tokens via Tailwind utilities only**:
  - Colors: `bg-card`, `text-muted-foreground`, `border-border-2` (semantic tokens from `@theme inline`)
  - Status semantics: `bg-ok-soft`, `text-ok-ink`, `bg-err-soft`, etc.
  - Surface layers: `bg-card`, `bg-muted`, `bg-muted-foreground/10`
  - Spacing: `gap-3`, `p-4`, `h-12` (Tailwind default scale; arbitrary only when needed: `gap-[12px]`)
  - Radii: `rounded-md`, `rounded-lg`, `rounded-full` (Tailwind default; arbitrary only for design-specific: `rounded-[12px]`)
  - Don't write raw color values like `bg-blue-500`, `text-[#abc]`, `oklch(...)`. Use tokens.

### CVA (class-variance-authority) for variants

```tsx
import { cva, type VariantProps } from 'class-variance-authority';

const buttonVariants = cva(
  'inline-flex items-center justify-center', // base
  {
    variants: {
      variant: {
        default: 'bg-primary text-primary-foreground',
        outline: 'border border-border bg-card',
        // ...
      },
      size: {
        default: 'h-9 px-4',
        sm: 'h-8 px-3 text-sm',
        // ...
      },
    },
    defaultVariants: { variant: 'default', size: 'default' },
  }
);
```

Plexor DS only adds variants when the design system needs them. Don't add variants speculatively.

## Composition

Compose with shadcn-ui primitives:

```tsx
import { Button } from '@/shared/ui/primitives/button';
import { InputGroup, InputGroupAddon, InputGroupInput } from '@/shared/ui/primitives/input-group';

export function MySearchInput() {
  return (
    <InputGroup>
      <InputGroupAddon>
        <SearchIcon />
      </InputGroupAddon>
      <InputGroupInput placeholder="Search..." />
    </InputGroup>
  );
}
```

When composing:
- Use shadcn `Field` for forms (not raw `div` with `space-y-*`).
- Use shadcn `ButtonGroup` for segmented controls.
- Use shadcn `InputGroup` for input with addons.
- Use shadcn `Empty` for empty states.
- Use shadcn `Skeleton` for loading.

## Component Showcase Conventions

The /components page (`src/routes/components.tsx`) has:
- Topnav with `<ModeToggle />` (light/dark/system)
- Sidebar nav with anchor links
- Main content with sections per component

When adding a new component:
1. Create the file in `src/shared/ui/primitives/<Name>.tsx`
2. Add to the barrel `src/shared/ui/primitives/index.ts` (or rebuild via `bunx tsr generate`)
3. Add a section to `src/routes/components.tsx` with a `<Demo>` wrapper
4. Add a sidebar entry to the `NAV` array
5. Group by category (Foundations / Buttons / Forms / Layout / Overlays / Data display / Feedback)

## Bulk-Action Toolbar (specific case)

A "bulk-action toolbar" appears in a table when rows are selected:

```tsx
// In the table page:
function TenantsPage() {
  const [selected, setSelected] = useState<string[]>([]);

  return (
    <>
      {selected.length > 0 && (
        <BulkActionToolbar
          count={selected.length}
          onClear={() => setSelected([])}
          actions={[
            { label: 'Suspend', onClick: () => {}, variant: 'outline' },
            { label: 'Delete', onClick: () => {}, variant: 'destructive' },
          ]}
        />
      )}
      <Table>
        ...with checkbox per row that toggles selected[i]...
      </Table>
    </>
  );
}
```

Visual:
```
┌──────────────────────────────────────────────────────────────┐
│  3 selected   [Suspend]  [Delete]              Clear selection  │
└──────────────────────────────────────────────────────────────┘
```

Built with shadcn `Button` (variants outline, destructive) + shadcn `Badge` (for count) + Plexor DS spacing.

When implementing, abstract to `BulkActionToolbar` if used in 2+ tables. Otherwise inline.

## URL Sync (TanStack Router)

For filter state that should be bookmarkable:

```tsx
import { useNavigate, useSearch } from '@tanstack/react-router';

function MyPage() {
  const search = useSearch({ from: Route.fullPath });
  const navigate = useNavigate({ from: Route.fullPath });
  
  const status = (search.status as string) ?? 'all';
  const setStatus = (next: string) => {
    navigate({ search: (prev) => ({ ...prev, status: next === 'all' ? undefined : next }) });
  };
}
```

Use `useSearch` (read) and `navigate({ search })` (write). Search params persist across reloads, are bookmarkable, and can be deep-linked.

## Theme Support

Plexor DS has light/dark via `html.dark` class. Always include both:
- Test in light mode (browser DevTools)
- Test in dark mode (use `<ModeToggle />` in Topnav to switch)

Use semantic color tokens that work in both modes:
- `bg-card` / `bg-muted` / `bg-background` — work both modes
- `text-foreground` / `text-muted-foreground` — work both modes
- `text-primary` / `text-primary-foreground` — work both modes

Avoid hardcoded `dark:` overrides (`dark:bg-...`). Use semantic tokens.

## Reference: Existing Custom Primitives

| Primitive | Tokens used | Pattern |
|---|---|---|
| `StatusPill` | `--ok-soft`, `--ok`, `--ok-ink`, etc. | data-slot, status enum → token mapping |
| `MonoNum` | `font-mono` + `tabular-nums` (Tailwind) | data-muted attr for variant |
| `Stat` | shadcn `Card` + custom trend indicator | data-trend attr |
| `Console` | inline `oklch(18% 0.02 240)` (hardcoded dark) | data-slot for state |
| `Toolbar` | flex layout + token colors | composite of Toolbar* subcomponents |

## Workflow

1. **Check shadcn first** — `bunx --bun shadcn@latest search @shadcn -q "<need>"`
2. **Check existing primitives** — `ls src/shared/ui/primitives/`
3. **Decide**: is this abstract (2+ use cases)? Then make a primitive. Otherwise, inline.
4. **Build**:
   - File: `src/shared/ui/primitives/<Name>.tsx`
   - Tokens: use only Plexor DS tokens (semantic Tailwind utilities)
   - Variants: cva for 3+ variants, plain function for 1-2
   - Slots: `data-slot="<name>"` on root
5. **Showcase**: add to `src/routes/components.tsx` with a `<Demo>`
6. **Verify**: `bun run build` (TypeScript catches type issues), test in browser

## Anti-patterns (DON'T)

- ❌ `className="p-4 rounded-md bg-white text-black"` — raw colors, no tokens
- ❌ `<div className="space-y-4">` — use `flex flex-col gap-4`
- ❌ Inline `<style>` blocks in component files
- ❌ New CSS classes in `index.css` for layout (`@layer utilities { .kpi { ... } }`)
- ❌ One-off component for a single use case (inline Tailwind + shadcn instead)
- ❌ `dark:bg-...` overrides (use semantic tokens)
- ❌ Hardcoded colors in component (`bg-[#abc]`, `bg-oklch(0.5 0.1 200)`)

## TL;DR

- New component in `src/shared/ui/primitives/<Name>.tsx` ONLY if abstract + 2+ use cases
- Pure Tailwind utilities + Plexor DS tokens (never raw colors, never `dark:` overrides)
- Compose with shadcn primitives; never reinvent
- `data-slot` for state queries elsewhere
- Add to /components showcase with `<Demo>` wrapper
- Build with `bun run build` (TS catches errors)
