# Recipe: new component

Use this when a page needs a UI piece that doesn't exist yet in
`src/shared/ui/primitives/`. Read `web/apps/console/AGENTS.md` first.

Most of the time you do NOT need this recipe — you need
`src/shared/ui/INDEX.md`. Read that first. This recipe is only for the
rare case where INDEX.md confirms nothing fits.

All commands below run from `web/apps/console`.

## 1. Check INDEX.md — exhaust it before creating anything

Open `src/shared/ui/INDEX.md`. It's a table: "you need X" → "use Y" →
"never Z". If a row is close but not exact, that component probably has a
variant prop you're missing — check the component file before assuming you
need a new one.

Also check the "Deleted" list in INDEX.md's appendix — if your need matches
something already deleted (drawer, calendar, pagination, command palette),
restore it from git history instead of rewriting it from scratch.

## 2. Decide: is this really a new primitive?

Create a new file in `src/shared/ui/primitives/` ONLY if ALL of:

- It's abstract — used (or clearly about to be used) in 2+ unrelated
  places.
- It carries its own semantics, not just a style tweak.
- shadcn/the existing primitives genuinely don't cover it.

Otherwise: inline Tailwind + composition of existing primitives, in the
feature file that needs it. A one-off layout is NOT a new primitive.

## 3. Build it — copy the shape of `button.tsx`

Open `src/shared/ui/primitives/button.tsx` as your template. Required
shape for any new primitive:

- File name = export name, PascalCase: `src/shared/ui/primitives/Toolbar.tsx`
  exports `Toolbar`.
- `data-slot="<kebab-name>"` on the root element.
- `cva()` for 3+ variants; a plain function for 1-2.
- `cn()` (from `@/lib/utils`) for all conditional classNames — never
  template-literal ternaries.
- Props extend `ComponentProps<'element'>`.
- Named export only. No default export.
- Icons (if any): import from
  `@nine-thirty-five/material-symbols-react/rounded/700`, never elsewhere.
- Colors: token utilities only (`bg-card`, `text-muted-foreground`,
  `bg-ok-soft`, …) — never raw hex/oklch/`bg-blue-500`.
- No `<style>` blocks, no new `@layer utilities` classes in `index.css`.

## 4. Story file

Add `<Name>.stories.tsx` next to the component. Copy
`src/shared/ui/primitives/button.stories.tsx`:

- `title: 'Primitives/<Name>'`.
- A `Default` story with realistic props (not empty/placeholder text).
- One story per variant axis that matters (sizes, variants, states like
  disabled) — see `Variants`, `Sizes`, `IconButtons`, `Disabled` in
  `button.stories.tsx`.

## 5. Add the row to INDEX.md

Add one row under the right section (Inputs / Selection / Overlays /
Layout / Feedback / Data display / Navigation / App shell). Follow the
existing row shape: "you need" / "use" / "never" / "notes". A primitive
missing from INDEX.md does not exist as far as the next agent is
concerned.

## 6. Shot both themes

```
bun run shot stories                              # confirm the story id
bun run shot story primitives-<name>--default --theme both
bun run shot story primitives-<name>--<other-variant> --theme both
```

Fix every issue the report lists, re-run until `PASS`, then:

```
bun run agent:check
```
