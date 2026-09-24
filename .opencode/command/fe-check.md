---
description: Run the DoD gate and fix everything until it passes
agent: fe
---

Run the Definition of Done gate for `web/apps/console` and fix everything
it flags. $ARGUMENTS

Steps:

1. Read `web/apps/console/AGENTS.md` if you haven't this session, in
   particular the "Definition of Done" and "When you get stuck" sections.
2. Run `bun run agent:check`.
3. If the last line is `AGENT-CHECK: PASS`, you're done — report it.
4. If it's `AGENT-CHECK: FAIL (...)`, read
   `web/apps/console/docs/agent/report-format.md` to understand which
   gate(s) failed. Fix the specific gate first (e.g. `bun run typecheck`
   alone, or `bun run shot page <path>` alone) rather than re-running the
   whole check every time.
5. Repeat step 2-4 until `AGENT-CHECK: PASS`.
6. Report using the final-answer format in your agent instructions.

If the same gate keeps failing on the same issue after 3 fix attempts,
stop and report what you tried — do not keep looping.
