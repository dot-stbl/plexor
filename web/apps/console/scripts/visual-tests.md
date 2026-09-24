# Visual regression pipeline

Every "посмотри, как выглядит" the user reports should be answerable by
reading a committed baseline PNG (or its diff against current render) —
**not** by writing a one-off Playwright repro.

This document covers:

- What the pipeline is and how it fits into the existing test stack.
- How to add a new visual test for a component.
- The **CI / local pixel-rendering caveat** (the part that bites if you
  don't internalise it).
- Where to look when a visual bug is reported.
- Cleanup discipline (the agent-runtime-safety interaction).

For the **why** — why this exists, what trade-offs it makes — see the
"Pipeline philosophy" section at the bottom.

---

## TL;DR

| Action | Command |
|---|---|
| Run visual tests against committed baselines | `bun run test:visual` |
| Regenerate baselines (CI does this on first green) | `bun run test:visual:update` |
| Clean up diff PNGs / test-results without running tests | `bun run test:visual:cleanup` |
| Inspect a diff | open `web/apps/console/.storybook/__screenshots__/__diff_output__/<id>.diff.png` |

Baselines live at:

```
web/apps/console/.storybook/__screenshots__/<story-id>.png
```

### `shot` vs `test:visual` — not the same tool

`bun run shot page|story ...` (see `AGENTS.md`, `docs/agent/`) is a
**separate, disposable** tool for an ad-hoc "let me look at this" —
one-off renders + a text report (issues, aria outline, visible text) in
`.shots/`, which is gitignored and never committed. It is **not** a
regression check: it has no baseline, no pass/fail-on-diff, and no CI
step.

`bun run test:visual` (this document) is the **regression** check: it
compares against the committed baselines in
`.storybook/__screenshots__/` and is what CI runs on every PR.

Use `shot` while building/debugging a page. Use `test:visual` only when
adding or intentionally changing a story's committed baseline (see
"Adding a new visual test" below) — an agent does not regenerate or
commit baselines as part of routine UI work.

Story IDs look like `primitives-button--variants` — `<kind>--<name>`,
the same ID Storybook shows in the URL bar of any story page.

---

## Architecture

```
.bun run test:visual
       │
       ▼
scripts/visual-test-runner.ts
       │
       ├─→ bun run build:storybook         # static build → dist-storybook/
       ├─→ bun run scripts/serve-static.ts # serves dist-storybook/ on :6006
       ├─→ bunx test-storybook             # Storybook test-runner + Jest + Playwright
       │       │
       │       └─→ .storybook/test-runner.ts
       │             │
       │             ├─→ preVisit: pin viewport + disable animations
       │             ├─→ story renders in Chromium
       │             └─→ postVisit: page.screenshot() → jest-image-snapshot
       │
       ├─→ cleanup: kill server, remove test-results/ + diff_output/
       └─→ exit with test-runner's exit code
```

Files involved:

| File | Role |
|---|---|
| `.storybook/test-runner.ts` | TestRunnerConfig — viewport, animations, screenshot hook |
| `.storybook/__screenshots__/` | Committed PNG baselines + diff output |
| `test-runner-jest.config.js` | Jest config override (rootDir + testRegex) |
| `scripts/serve-static.ts` | Tiny Bun static-file server for `dist-storybook/` |
| `scripts/visual-test-runner.ts` | Orchestrator: build → serve → test → cleanup |
| `.github/workflows/web.yml` | `visual-tests` job — runs after `web`, uploads diffs on failure |
| `~/.agents/rules/process/agent-runtime-safety.md` | Single-run + cleanup + no-blanket-kill discipline |

---

## CI / local pixel-rendering caveat

**Baselines are CI-generated on `ubuntu-latest`.** They use the Linux
Chromium build with the fonts and rendering pipeline shipped on GitHub
Actions. If you run `bun run test:visual` on Windows or macOS, you may
see false-positive pixel diffs even when the rendered output is visually
identical — sub-pixel anti-aliasing differences, font hinting, fractional
pixel rounding, all of it.

What this means in practice:

1. **The committed baselines are the source of truth.** Don't regenerate
   them locally — the diffs you'll see may not match what CI sees.
2. **If CI goes red on a visual test, trust the diff in the artifacts.**
   The `visual-test-diffs` artifact on a failed run contains the actual
   diff PNGs that CI computed; download it, eyeball the diff. If the
   diff looks like noise (1-2 px shifts, anti-aliasing), bump the
   `failureThreshold` in `.storybook/test-runner.ts` from `0.01` to
   something the team agrees on.
3. **When you intentionally change a story's render, regenerate the
   baseline on CI, not locally.** The CI workflow does not auto-update
   — a human has to push the regenerated PNG. The cleanest path:
   - Land your component change in a PR.
   - The `visual-tests` job fails on that PR with a clear diff artifact.
   - You confirm the diff is intentional, then push the regenerated
     baseline in a follow-up commit:
     ```
     bun run test:visual:update
     git add web/apps/console/.storybook/__screenshots__
     git commit -m "[.stbl](feat/<area>): regenerate <story> baseline after <change>"
     ```
4. **`failureThreshold: 0.01` (1% pixel diff)** is the floor. Below
   that, false positives from font rendering start to drown the signal.
   If you find yourself wanting to lower it, the story is probably
   doing something time-dependent (animation, random data) and should
   be fixed at the story level, not by widening the threshold.

This whole caveat is the reason the prompt says **"baselines are
CI-generated, local runs may differ"**. Don't fight it.

---

## Adding a new visual test

1. **Write the story.** Co-locate `<Component>.stories.tsx` next to
   `<Component>.tsx` in `src/shared/ui/primitives/` (or wherever the
   component lives). Use a stable `title` (e.g. `Primitives/<Name>`).

2. **Add at least one variant.** A bare `<Component />` story isn't
   worth a baseline — it's just an empty snapshot. Render the realistic
   shape (icons, states, sizes) you'd want a reviewer to eyeball.

3. **Generate the baseline locally** (eyeball it, then push to CI):
   ```bash
   bun run test:visual:update
   git diff web/apps/console/.storybook/__screenshots__
   ```
   The diff will include both your new baseline and a regenerated copy
   of every existing baseline (Windows pixel noise — that's the caveat).

4. **Reset the existing baselines before pushing** (since the diff will
   look noisy locally — keep only the new files):
   ```bash
   git checkout web/apps/console/.storybook/__screenshots__
   # then add ONLY the new files
   git add web/apps/console/.storybook/__screenshots__/<new-id>.png
   ```

5. **Land the PR with the new story + new baseline.**

6. **On CI, the new baseline gets confirmed.** The diff artifacts in the
   `visual-test-diffs` upload let you see what local-vs-CI noise looks
   like; if it's all anti-aliasing, ignore. If CI now shows a real
   diff for one of your existing stories, push a regenerated baseline.

In short: **write the story locally; baseline locally; review the
local-vs-CI diff on first green; commit only the new baseline.**

---

## Where to look when the user reports a visual bug

The user says something like "the dialog is broken" or "the tooltip is
in the wrong place" or "select dropdown cuts off".

1. **Find the story.** The component lives somewhere in `src/`; the
   story is `<Component>.stories.tsx` next to it. If there's no story,
   this pipeline can't help — write one (see above).

2. **Open the story in dev mode** to see what it looks like in your
   browser: `bun run storybook`. Eyeball vs the user's report.

3. **Diff against the committed baseline:**
   ```bash
   bun run test:visual
   ```
   This produces `__diff_output__/<story-id>.diff.png` in
   `.storybook/__screenshots__/`. The diff is:
   - **red** for pixels the new render has but the baseline didn't,
   - **green** for pixels the baseline had but the new render doesn't,
   - **gray** for unchanged pixels.

4. **Compare the diff against the user's complaint.** If the user's
   complaint matches a region of the diff, you've found the visual
   regression — fix the component, regenerate the baseline. If the
   diff is empty / noise, the bug isn't a visual regression in this
   pipeline's coverage — dig deeper (specific viewport, specific
   state, dynamic content).

---

## Cleanup discipline (agent-runtime-safety)

Per `~/.agents/rules/process/agent-runtime-safety.md`, browser automation
is now allowed with discipline. The visual pipeline satisfies that:

1. **Single-run, exit-on-completion.** `bun run test:visual` and
   `bun run test:visual:update` exit when done. There is no
   `--watch`, no `bun run storybook &` left dangling, no
   long-lived browser.
2. **Cleanup on exit.** The orchestrator (`scripts/visual-test-runner.ts`)
   kills the static server in `finally` and removes `test-results/`,
   `playwright-report/`, and `__diff_output__/`. If the orchestrator
   crashes (SIGKILL, terminal close), these directories may leak —
   `bun run test:visual:cleanup` cleans them up.
3. **No blanket process kills.** The orchestrator tracks the server PID
   exactly and kills by PID only. **Never** run `taskkill //F //IM
   node.exe`, `pkill node`, or `pkill -9 node` from this pipeline —
   it kills the agent's own Node runtime (see the 2026-07-07 incident
   documented in agent-runtime-safety.md).
4. **Structured pipeline, not ad-hoc browser launches.** The agent
   does NOT improvise `chromium.launch()` / raw Playwright to look at
   one story. **Superseded (2026-09):** the sanctioned way to look at
   ad-hoc rendered output is now `bun run shot page|story ...` (see
   `AGENTS.md`, `docs/agent/`) — a wrapped, single-run, self-cleaning
   script, not a hand-rolled browser session. It does not touch this
   pipeline's committed baselines; see "`shot` vs `test:visual`" above.
   If a *regression* baseline genuinely needs updating, write/adjust the
   story, regenerate that one baseline, commit — same as before.

---

## Pipeline philosophy

**Why this exists.** Before this pipeline, the only way to investigate
a "посмотри, как выглядит" report was: the agent launches playwright,
points it at a dev server, takes a screenshot, eyeballs it, fixes
something based on what they saw. That breaks the "agent can't trust
agent's own claims" feedback loop — the agent is the only one who
looked at the bug and the agent is also the one declaring it fixed.

With this pipeline, **the bug is captured at the moment the user
reports it** (the diff PNG), and **the fix is verified against the
committed baseline** by CI, not by the agent. Anyone — the user, the
agent, a reviewer — can read the diff and decide if the change is
intended or a regression.

**Why Storybook test-runner.** Storybook already ships; we already
have 8 stories; the test-runner is one dep + one config file away.
Chromatic / Percy / Playwright Test + visual-regression plugins are
all options but they add a third-party service (Chromatic), a paid
tier (Percy), or a parallel test infrastructure (Playwright Test +
plugin). Storybook's own runner is good enough for "this PNG looks
the same as last week's PNG" — which is the actual question being
asked.

**Why 1% threshold.** Below 1%, font rendering noise dominates. Above
1%, real regressions (missing element, wrong color, layout shift of
>10px) still trip it. If we need finer, we address it story-by-story
(custom `failureThreshold` per call to `toMatchImageSnapshot`).

**Why single viewport.** Most visual bugs at this scale are component
shape bugs — wrong padding, wrong color, broken portal. Those show up
at 1280×800. Mobile-specific bugs come from layout shifts, which we
can add as a follow-up if/when they bite. The `--browsers` flag on
`test-storybook` makes adding `firefox` / `webkit` a one-line config
change when needed.
