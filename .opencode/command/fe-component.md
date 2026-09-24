---
description: Add a new UI primitive to the Plexor console design system
agent: fe
---

Build this component in `web/apps/console`: $ARGUMENTS

Steps:

1. Read `web/apps/console/AGENTS.md` if you haven't this session.
2. Read `web/apps/console/docs/agent/new-component.md` and follow it
   exactly. First check `src/shared/ui/INDEX.md` — most "new component"
   requests turn out to already be covered by an existing primitive plus a
   prop. Only build a new file if INDEX.md confirms nothing fits and the
   need is genuinely abstract (2+ use cases).
3. Follow the `button.tsx` pattern: `data-slot`, `cva` for variants, `cn()`
   for classes, named export only, icons from
   `@nine-thirty-five/material-symbols-react/rounded/700`, token colors
   only.
4. Add a `.stories.tsx` file with realistic variants and add the new row to
   `src/shared/ui/INDEX.md`.
5. Run THE LOOP: `bun run shot story <id> --theme both` for every story
   variant until `PASS`, then `bun run agent:check` until
   `AGENT-CHECK: PASS`.
6. Report using the final-answer format in your agent instructions.

If you get stuck on the same issue 3 times, stop and report what you tried
— do not keep looping.
