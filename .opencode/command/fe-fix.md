---
description: Debug and fix a visual/behavioral bug — /fe-fix <route or story> <what is wrong>
agent: fe
---

Fix this in `web/apps/console`: $ARGUMENTS

The first token is the route path (e.g. `/vms/abc-123`) or story id (e.g.
`primitives-select--default`). Everything after it describes what's wrong.

Steps:

1. Read `web/apps/console/AGENTS.md` if you haven't this session.
2. Read `web/apps/console/docs/agent/visual-debug.md` and follow it
   exactly — reproduce with `bun run shot`, add `--click`/`--hover`/
   `--fill`/`--mobile`/`--theme` steps to match the reported bug, then read
   the issue codes and the aria-outline/visible-text sections of the
   report (you may not be able to see the PNG).
3. Use the issue-code table in that recipe to find the likely cause. If
   the report doesn't name an issue code that matches, bisect: strip the
   component down, re-shot, find what makes the bug disappear.
4. Fix the code. Re-run `bun run shot` on the same target(s) until `PASS`.
5. Run `bun run agent:check` until `AGENT-CHECK: PASS`.
6. Report using the final-answer format in your agent instructions.

If you get stuck on the same issue 3 times, stop and report what you tried
— do not keep looping.
