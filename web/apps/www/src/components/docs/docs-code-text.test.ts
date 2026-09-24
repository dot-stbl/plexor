import { createElement } from 'react';
import { describe, expect, it } from 'vitest';
import { extractCodeText } from './docs-code-text';

describe('extractCodeText', () => {
  it('returns a plain string unchanged', () => {
    expect(extractCodeText('git clone …')).toBe('git clone …');
  });

  it('joins an array of nodes', () => {
    expect(extractCodeText(['line 1\n', 'line 2'])).toBe('line 1\nline 2');
  });

  it('unwraps a single <code> element (the real MDX shape)', () => {
    const code = createElement('code', { className: 'language-bash' }, '$ plx init');
    expect(extractCodeText(code)).toBe('$ plx init');
  });

  it('returns an empty string for null/undefined/booleans', () => {
    expect(extractCodeText(null)).toBe('');
    expect(extractCodeText(undefined)).toBe('');
    expect(extractCodeText(true)).toBe('');
  });
});
