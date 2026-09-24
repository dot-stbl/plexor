/**
 * Pure helpers for time-driven "reveal" effects (typewriter, count-up) —
 * no DOM, no React, covered directly by `timed-reveal.test.ts`. Shared
 * by the bento grid's live mini-visuals (`bento/bento-visuals.tsx`) and
 * the "Get running" terminal (`marketing-install.tsx`), so both type
 * text and count numbers up with the exact same clock math.
 */

/** Number of characters visible after `elapsedMs` at a fixed `msPerChar` typing rate, clamped to `[0, textLength]`. */
export function typedLength(textLength: number, elapsedMs: number, msPerChar: number): number {
  if (msPerChar <= 0) return textLength;
  return Math.max(0, Math.min(textLength, Math.floor(elapsedMs / msPerChar)));
}

/**
 * Splits `lines` by a single running character budget (`visibleChars`),
 * as if they were typed as one continuous stream with an implicit
 * newline between each — so a multi-line terminal types line 1, then
 * line 2, rather than every line growing in lockstep. Lines beyond the
 * budget are omitted entirely (not returned as empty strings).
 */
export function typedLinesUpTo(lines: readonly string[], visibleChars: number): readonly string[] {
  const result: string[] = [];
  let consumed = 0;
  for (const line of lines) {
    const remaining = visibleChars - consumed;
    if (remaining <= 0) break;
    result.push(line.slice(0, remaining));
    consumed += line.length + 1; // +1: the implicit newline between lines.
  }
  return result;
}

/** Linear count from 0 to `target` over `durationMs`, rounded to the nearest integer, clamped to `target`. */
export function countUp(target: number, elapsedMs: number, durationMs: number): number {
  if (durationMs <= 0) return target;
  const progress = Math.max(0, Math.min(1, elapsedMs / durationMs));
  return Math.round(target * progress);
}
