# Recipe: reading `shot` and `agent:check` output

Use this whenever you're not sure what a `bun run shot` or `bun run
agent:check` result means. Read `web/apps/console/AGENTS.md` first.

## `shot` stdout — one line per target

Each page/story/theme combination prints one summary line, then zero or
more issue lines:

```
FAIL  page /vms/abc-123 (light)  png: .shots/page/vms-abc-123.light.png  report: .shots/page/vms-abc-123.light.md
  - [console-error] TypeError: Cannot read properties of undefined (reading 'name')
    fix: guard `vm.name` behind the isPending/isError check before rendering
  - [no-accessible-name] button has no accessible name
    fix: add aria-label to the icon-only Button in the lifecycle card

WARN  page /vms/abc-123 (dark)  png: .shots/page/vms-abc-123.dark.png  report: .shots/page/vms-abc-123.dark.md
  - [i18n-missing] key "vms.detail.field.diskEncrypted" not found

PASS  story primitives-button--variants (light)  png: .shots/story/primitives-button--variants.light.png  report: .shots/story/primitives-button--variants.light.md

SHOT: 1 pass, 1 warn, 1 fail
```

- **PASS** — nothing to do.
- **WARN** — not blocking on its own, but read it. A `WARN` on an i18n key
  or a console warning is often a real bug — fix it unless you have a
  specific reason not to (say why in your final answer if you skip it).
- **FAIL** — must fix before moving on. Each issue line has a `code` in
  brackets and, when there's an obvious one, a `fix:` hint on the next
  line. The `SHOT:` totals line is the thing to check first — if
  `n fail > 0`, you're not done.

### Duplicate issues collapse with `(×N)`

Issues with the same severity, code, AND message (e.g. the same console
warning firing once per row in a table) are deduped into a single line with
a `(×N)` suffix instead of printing N identical lines:

```
  - [WARN console-warn] If you do not provide a visible label, you must specify an aria-label or aria-labelledby attribute for accessibility @ /node_modules/.vite/deps/react-aria-components.js?v=...:4965 (×8)
    fix: A react-aria control (SearchField/TextField/Select/Checkbox/Slider…) on this page has no label. …
```

No `(×N)` suffix means the issue happened exactly once. stdout shows at most
10 deduped issue lines per target, then a line like `… 3 more, see report`
— the per-target `.md` report always has the full deduped list, never
capped. `.shots/LAST.md` follows the same dedupe + 10-line cap as stdout
(with the `fix:` lines omitted to stay short); it's an index, the `.md`
report next to each PNG is the source of truth.

## The per-target `.md` file

Each PNG has a matching `.md` file with more detail than stdout:

1. **Issues** — same deduped list as stdout (see "Duplicate issues collapse
   with `(×N)`" above), but never capped — if stdout showed `… N more, see
   report`, this file is where those N live.
2. **Aria structure outline** — the accessible tree: roles and accessible
   names, nested by DOM structure. Read this to confirm an element exists
   and is labeled correctly without looking at the image.
3. **Visible text** — every text node that rendered, in document order.
   A raw i18n key showing up here (e.g. `vms.detail.title` instead of "VM
   detail") means that key is missing from `en/common.json`.

`.shots/LAST.md` holds the result of the most recent `shot` invocation —
check it if you lost track of which target you last ran.

## Worked example: a FAIL, and what to do

```
FAIL  page /networks (light)  png: .shots/page/networks.light.png  report: .shots/page/networks.light.md
  - [page-hscroll] page scrolls horizontally at 1280px viewport
    fix: check for a fixed-width child wider than its container
  - [i18n-missing] key "networks.table.bindings" not found
```

Steps, in order:

1. Open `.shots/page/networks.light.md`. Check "Visible text" — if you see
   the raw key `networks.table.bindings` printed instead of a real header
   label, that confirms the i18n miss. Add the key to `en/common.json` AND
   `ru/common.json`.
2. For `page-hscroll`, the message already names the scrolling pane and, as
   "Widest child", the specific offending element (e.g. a fixed `w-[...px]`
   div) — open the component file it points to and remove the fixed width
   or add `min-w-0` to the flex child. Fix it.
3. Re-run: `bun run shot page /networks --theme both`.
4. Confirm the totals line reads `0 fail` before moving on.

## `agent:check` output

`bun run agent:check` runs typecheck, lint, vitest, the rules grep
(`agent:rules`), and `shot` against every route/story you changed. It
prints its own progress, then one final line:

```
AGENT-CHECK: PASS
```

or

```
AGENT-CHECK: FAIL (typecheck: 2 errors, shot: 1 fail)
```

The `FAIL (...)` line names which gate(s) failed. Fix those specific
gates first — re-running the single failing command (e.g.
`bun run typecheck`) is faster than re-running the whole check on every
iteration. Run `bun run agent:check` again before you say you're done;
never assume a fix worked without seeing the final `PASS`.

## Known pre-existing issues

- **`/images` (any theme): `console-warn` "If you do not provide a visible
  label, you must specify an aria-label or aria-labelledby attribute for
  accessibility" (×8).** Root cause: `src/shared/ui/primitives/select.tsx`'s
  `PlexorSelectRoot` never forwards a `label`/`aria-label`/`aria-labelledby`
  to the underlying react-aria-components `<Select>` root — every
  `<Select>` on the page (the "Arch" and "Видимость" filter dropdowns in
  `data-table-toolbar.tsx`) triggers react-aria's own internal dev-mode
  warning, even though the visible trigger button already reads fine
  ("Arch"/"Видимость" show up as accessible names in the aria outline
  because a `role="button"` gets its name from its own text content — the
  warning is about the `Select` root's internal field wiring, not the
  button you can see). This is **app source** (`src/shared/ui/primitives/`)
  — out of scope for the agent-tooling changes in this repo pass, not
  fixed here. The `no-accessible-name` DOM check (see
  `visual-debug.md`'s issue-code table) does **not** catch this specific
  case either: the affected element is the `<Select>` root's hidden native
  form control, which doesn't match any of the checked selectors (`input`,
  `[role=combobox]`, `[role=slider]`, `[role=checkbox]`, `[role=switch]`,
  `[role=searchbox]`, `[role=textbox]`) — so you'll only ever see this as
  the deduped `console-warn` line, never as a `no-accessible-name` line
  with a `describe(el)`. If you do fix it: add `label`/`aria-label` to
  each `<Select>` usage (the `SelectFilter` in `data-table-toolbar.tsx` is
  the one behind `/images`), not to the DOM-check selector list.
