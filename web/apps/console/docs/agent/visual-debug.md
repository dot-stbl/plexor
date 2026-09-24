# Recipe: visual debug ("this looks wrong")

Use this when a route or component renders wrong — misaligned, cut off,
wrong color, broken interaction, or a user/report says "looks broken".
Read `web/apps/console/AGENTS.md` first.

All commands below run from `web/apps/console`.

## 1. Reproduce it with `shot`

```
bun run shot page /vms/abc-123 --theme both
```

If the bug only shows up after an interaction, chain steps in the order
they happen:

```
bun run shot page /vms --click "text=Create" --fill "role=textbox[name=\"Name\"]=test-vm" --press Enter --wait 300
bun run shot page /vms --hover "text=prod-eu-1" --theme dark
bun run shot page /vms --mobile
```

Steps run in the order given on the command line. Use `--theme both` unless
the report is specifically about one theme. Use `--mobile` if the report
mentions phone/narrow layout.

## 2. Read the issue codes

| Code | Typical cause | Fix |
|---|---|---|
| `console-error` | A React/JS error was logged (often a bad prop, a key warning treated as error, or a rejected promise) | Read the exact message in the report; fix the code path it names |
| `console-warn` | Deprecated prop, missing `key`, an `act()`/testing warning | Same — the message text names the file/line |
| `uncaught-exception` | Component read `data.field` before checking `isPending`/`isError` | Guard rendering behind the loading/error checks (see `vms/$id.tsx`) |
| `request-failed` | No MSW handler for that endpoint/method | Add it to `src/shared/api/mocks/handlers.ts`, or a handmade module if it's not in the contract yet |
| `http-<status>` | e.g. `http-404`: the id in your `shot` command doesn't match a mock row. `http-500`: the handler/fixture threw | Use an id that exists in the mock data, or fix the fixture |
| `blank` | Nothing painted — route/story threw before first render, or the path is wrong | Run `bun run shot routes` (or `stories`) to confirm the exact path/id spelling |
| `router-error` | Route param missing, wrong type, or the route isn't registered | Check `$id`-style params against `bun run shot routes` sample ids |
| `page-hscroll` | The page OR a scroll pane inside it (e.g. the app-shell's `[data-od-id="app-content"]`) scrolls sideways — a child has a fixed `w-[...px]`, a `min-w-*`, or a flex row is missing `min-w-0`. The message names the pane and, as "Widest child", the specific offending element. Tables/tabs/`pre`/`code`/terminal panes are recognized as intended horizontal scrollers and never flagged | Add `min-w-0`/`truncate` to the "Widest child" named in the message, or drop the fixed width; wide tables should stay in their normal `overflow-x-auto` wrapper |
| `overflow-right` | A popup/select is wider than its trigger (Base UI `alignItemWithTrigger` default), or `scrollbar-gutter: stable` leaking into a popup | See "Select / Combobox / Popover — overlay pitfalls" in `.agents/rules/web-frontend.md` |
| `text-clipped` | `truncate`/`line-clamp` on a wrapper instead of the text node, or a fixed height too small for the font | Move `truncate` onto the `<span>` holding the text; check line-height against the fixed height |
| `no-accessible-name` | Icon-only button with no `aria-label`, OR a form/aria widget (`input`, `[role=combobox\|slider\|checkbox\|switch\|searchbox\|textbox]`) with no `aria-label`/resolved `aria-labelledby`/`<label for>` — the message includes `describe(el)` naming the exact element | Add `aria-label="<action>"` on the `<Button>`, or `aria-label`/a `<Label htmlFor>` on the widget named in the message |
| `i18n-missing` | `t('some.key')` used but the key isn't in `en/common.json` | Add the key to BOTH `en/common.json` and `ru/common.json` |
| `broken-image` | Bad `src`, or a `TechIcon` slug not present in `tech-icon-data.ts` | Fix the path, or use the Material-generic fallback for that slug |
| `step-failed` | A `--click`/`--hover`/`--fill` selector matched nothing | Re-check the visible text or `role=...[name="..."]` against the aria outline in the report — the element may not exist in this state |
| `story-error` | The story's `render()` threw (missing context/provider, wrong import) | Compare against a working story like `networks.stories.tsx`; check every import resolves |

## 3. No image vision? Use the report's text

Every report includes:

- **Aria structure outline** — the accessible tree (roles + names). Use
  this to confirm an element exists and has the right label, without
  looking at the PNG.
- **Visible text** — every text node that rendered. Use this to confirm
  copy, i18n keys resolved (a raw key like `vms.detail.title` appearing
  here means `i18n-missing`), and that the right content is present.

Read these two sections before touching the PNG. Most layout bugs (wrong
element rendered, missing button, unresolved i18n key) are diagnosable from
text alone.

## 4. Compare against the committed baseline

If a *visual regression* test (`bun run test:visual`) exists for the same
story, its committed baseline is in
`.storybook/__screenshots__/<story-id>.png`. Compare your `shot` PNG
against it — same viewport, same story — to see exactly what moved. Don't
touch the baseline itself (see `scripts/visual-tests.md`); `shot` is a
separate, disposable tool for this kind of ad-hoc look.

## 5. Bisect when the cause isn't obvious

1. Temporarily strip the page/component down to the smallest JSX that
   still reproduces the bug (comment out sections, not delete).
2. `bun run shot` again after each cut.
3. When the bug disappears, the last thing you removed is the cause.
4. Restore the rest, fix that one thing, `shot` again to confirm the fix
   didn't break what you removed.

Undo all temporary stripping before finishing — don't leave commented-out
JSX behind.

## 6. Confirm the fix

```
bun run shot page <path> --theme both
bun run agent:check
```

Both must be clean before you report done. See `report-format.md` for how
to read `agent:check`'s output.
