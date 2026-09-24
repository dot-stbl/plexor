---
description: Build a new list/detail/form page in the Plexor console
agent: fe
---

Build this page in `web/apps/console`: $ARGUMENTS

Steps:

1. Read `web/apps/console/AGENTS.md` if you haven't this session.
2. Read `web/apps/console/docs/agent/new-page.md` and follow it exactly —
   pick the matching exemplar (list / detail / form), copy its shape, build
   the route file, the `src/features/<name>/` folder, mock data if the
   endpoint doesn't exist yet, i18n keys in BOTH `en/common.json` and
   `ru/common.json`, and a page story covering the realistic states
   (default / empty / loading / error, as applicable).
3. Run THE LOOP from `web/apps/console/AGENTS.md`: `bun run shot page ...`
   and `bun run shot story ...` for every state, in both themes, until
   every target is `PASS`.
4. Run `bun run agent:check` until its last line is `AGENT-CHECK: PASS`.
5. Report using the final-answer format in your agent instructions (files
   changed, last `SHOT:` line, last `AGENT-CHECK:` line, open issues).

If you get stuck on the same issue 3 times, stop and report what you tried
— do not keep looping.
