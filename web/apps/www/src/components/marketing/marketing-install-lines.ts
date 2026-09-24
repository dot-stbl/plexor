import { CLONE_COMMAND, RUN_COMMAND, VERSION_LABEL } from '@/components/chrome/nav-config';

export interface TerminalLine {
  readonly text: string;
  readonly copyable: boolean;
}

/**
 * Quickstart terminal lines (spec §3.2) — the real clone + run path from
 * the root README's stack section, via `nav-config`'s verified
 * `CLONE_COMMAND` / `RUN_COMMAND` (never hardcoded here). The trailing
 * comment lines are explicit that there is no packaged install command
 * yet (`CONTENT-PLAN.md` §8 rule 2) and point at the real install doc
 * instead of inventing one.
 */
export const TERMINAL_LINES: readonly TerminalLine[] = [
  { text: `$ ${CLONE_COMMAND}`, copyable: true },
  { text: `$ ${RUN_COMMAND}`, copyable: true },
  { text: '', copyable: false },
  { text: '# once a release artifact exists, see:', copyable: false },
  { text: '# /docs/getting-started/install', copyable: false },
];

/**
 * Joins only the `$`-prefixed command lines (never the blank/comment
 * lines) into the string the copy button places on the clipboard,
 * stripping the leading `$ ` so a pasted line runs directly.
 */
export function copyableLines(lines: readonly TerminalLine[]): string {
  return lines
    .filter((line) => line.copyable)
    .map((line) => line.text.replace(/^\$\s*/, ''))
    .join('\n');
}

/**
 * The same two commands as `TERMINAL_LINES`' copyable lines, kept as a
 * plain string array for the "types itself" effect
 * (`timed-reveal.ts`'s `typedLinesUpTo`) — that helper types full lines
 * including the `$ ` prompt, so this is the `$`-prefixed form, not
 * `copyableLines`' stripped one.
 */
export const COMMAND_LINES: readonly string[] = [`$ ${CLONE_COMMAND}`, `$ ${RUN_COMMAND}`];

/**
 * The 2 lines shown once the commands finish typing — real, verifiable
 * text only: `VERSION_LABEL` (the same version chip shown in the header/
 * footer, never invented here) and the Generic Host's own startup
 * message (`Microsoft.Extensions.Hosting`'s well-known, version-stable
 * "Application started..." line — every ASP.NET Core app prints it; it
 * is not specific to this instance). No port number: Kestrel's bound
 * port isn't fixed in this repo, so stating one would be a guess, and no
 * timing claim (the brief rules both out as "fake").
 */
export const OUTPUT_LINES: readonly string[] = [
  `Plexor ${VERSION_LABEL}`,
  'Application started. Press Ctrl+C to shut down.',
];
