import { describe, expect, it } from 'vitest';
import { BENTO_CELLS, bentoStatusLabel, bentoStatusVariant } from './bento-data';

describe('bentoStatusLabel', () => {
  it('maps shipped to "Shipped"', () => {
    expect(bentoStatusLabel('shipped')).toBe('Shipped');
  });

  it('maps next to "Next"', () => {
    expect(bentoStatusLabel('next')).toBe('Next');
  });
});

describe('bentoStatusVariant', () => {
  it('maps shipped to the ok status variant', () => {
    expect(bentoStatusVariant('shipped')).toBe('ok');
  });

  it('maps next to the warn status variant', () => {
    expect(bentoStatusVariant('next')).toBe('warn');
  });
});

describe('BENTO_CELLS', () => {
  it('has six cells, each with a non-empty title and body', () => {
    expect(BENTO_CELLS.length).toBe(6);
    for (const cell of BENTO_CELLS) {
      expect(cell.title.length).toBeGreaterThan(0);
      expect(cell.body.length).toBeGreaterThan(0);
    }
  });

  it('has no duplicate ids', () => {
    const ids = BENTO_CELLS.map((cell) => cell.id);
    expect(new Set(ids).size).toBe(ids.length);
  });

  it('marks at least one cell as next (not everything shipped)', () => {
    expect(BENTO_CELLS.some((cell) => cell.status === 'next')).toBe(true);
  });
});
