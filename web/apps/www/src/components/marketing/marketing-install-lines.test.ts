import { describe, expect, it } from 'vitest';
import { TERMINAL_LINES, copyableLines } from './marketing-install-lines';

describe('copyableLines', () => {
  it('strips the leading "$ " and joins only the copyable lines', () => {
    const result = copyableLines(TERMINAL_LINES);
    const lines = result.split('\n');
    expect(lines).toHaveLength(2);
    for (const line of lines) {
      expect(line.startsWith('$')).toBe(false);
      expect(line.length).toBeGreaterThan(0);
    }
  });

  it('excludes comment and blank lines', () => {
    const result = copyableLines(TERMINAL_LINES);
    expect(result).not.toContain('#');
    expect(result.split('\n').some((line) => line.trim().length === 0)).toBe(false);
  });

  it('returns an empty string when nothing is copyable', () => {
    expect(copyableLines([{ text: '# comment', copyable: false }])).toBe('');
  });
});
