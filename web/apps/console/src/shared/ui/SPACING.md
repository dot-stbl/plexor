# Spacing & token standard — Plexor console

The checklist future agents (and the canon skill) audit against. If a review
comment cites "spacing", it cites this file. Last audited: 2026-09-24.

## 1. Rhythm ownership — primitives own padding, pages own layout

A primitive owns its **internal vertical rhythm** via CSS-variable padding
(`--card-spacing`-style). A page owns **layout** — how components stack and
stretch (flex / grid / `gap-*` on the page's own wrappers).

Concretely, for `Card` and every part (`CardHeader`, `CardContent`,
`CardFooter`):

- ✅ ALLOWED on card parts: semantic classes (`border-b border-border`),
  layout classes (`flex flex-col gap-2`, `grid grid-cols-2`).
- ❌ FORBIDDEN on card parts: `p-*`, `pt-*`, `px-*`, `pb-*`, `py-*`, `m-*`
  rhythm overrides. The primitive already pads; hand-padding stacks on top
  and produces double spacing.

The same rule applies to `SheetContent` / `DialogContent` /
`AlertDialogContent` / `EmptyState` / `Alert` — no page-level padding on
overlay or state primitives (sizing like `sm:max-w-lg` is fine, it is not
rhythm).

## 2. Card padding model

All values derive from one CSS variable set on the `Card` root:
`--card-spacing` (`--spacing(4)` = 16px default, `--spacing(3)` = 12px with
`size="sm"`). Vertical rhythm is **padding-driven** — every section carries
its own `pt`; the root has no `gap`. The root carries **`pb` only**: the
first section's own `pt` is the card's top inset, so top and bottom insets
are symmetric (a root `py` would double-pad the top — the regression this
model exists to prevent).

| Slot          | Base padding                                       |
|---------------|----------------------------------------------------|
| `Card`        | `pb-(--card-spacing)` — frame bottom inset; top    |
|               | inset comes from the first section's `pt`          |
| `CardHeader`  | `px` + `pt` (auto `pb` only when it has `border-b`)|
| `CardContent` | `px` + `pt`                                        |
| `CardFooter`  | `px` (auto `pt` only when it has `border-t`)       |

A bordered-header card therefore needs **zero** page-level padding classes:

```tsx
<Card>
  <CardHeader className="border-b border-border">
    <CardTitle>…</CardTitle>
  </CardHeader>
  <CardContent className="flex flex-col gap-2">…</CardContent>
</Card>
```

To change a card's density, use the sanctioned dials — in order:

1. `<Card size="sm">` — compact (12px).
2. `<Card className="[--card-spacing:--spacing(6)]">` — spacious (hero /
   marketing surfaces only).

Never hand-pad the parts; never zero the root (`gap-0 p-0` is dead syntax
from the pre-variable era).

## 3. Edge-to-edge exception — `CardContent className="p-0"`

The one documented padding override: children that must sit flush against
the card sides — `DataTable`, divided lists, terminal surfaces:

```tsx
<CardContent className="p-0">
  <div className="divide-y divide-border">…rows with their own px…</div>
</CardContent>
```

tailwind-merge resolves the base `pt-(--card-spacing)` against the page's
`p-0`. Put breathing room on the inner element (`p-6` empty state), not on
the part. If you reach for any other `p-0` / `pt-0` on a card part, stop and
document why in a code comment — the default rhythm is almost always right.

## 4. Token rules

- **No hex colors in `*.tsx`.** Colors come from utilities (`bg-muted`,
  `text-muted-foreground`) or CSS vars (`bg-(--terminal-bg)`). Hex lives in
  `index.css` (the token source) and in two blessed files: brand SVGs
  (`provider-icons.tsx`) and recharts attribute selectors (`chart.tsx`).
  Two non-hex literal forms are equally sanctioned: accent-preset **data**
  (the `oklch(…)` strings in `preferences-provider.tsx`,
  `preferences-dialog.tsx`, and the `branding.tsx` default fallback —
  user-selectable theme values written into `--accent` at runtime, not
  static styling) and `scroll-area.tsx`'s injected scrollbar CSS
  (`oklch(var(--token))` — token-derived by construction).
- **No arbitrary px spacing** (`p-[3px]`, `mt-[18px]`) where a spacing step
  exists — use the step (`p-0.75`, `mt-4`). The `[--var:…]` arbitrary
  *property* syntax is the token mechanism and is always correct.
- **Widths are measurements, not rhythm**: `meta.size` classes for DataTable
  columns (`w-[110px]`) and min/max-width constraints are exempt — they size
  content, they don't space it.
- **z-index uses the semantic scale** (`z-popover`, `z-sticky`, `z-modal`).
  No raw `z-[0-9]`.
