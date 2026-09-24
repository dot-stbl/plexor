import { describe, expect, it } from 'vitest';
import { countUp, typedLength, typedLinesUpTo } from './timed-reveal';

describe('typedLength', () => {
  it('is 0 before any time has elapsed', () => {
    expect(typedLength(10, 0, 20)).toBe(0);
  });

  it('grows one character per msPerChar', () => {
    expect(typedLength(10, 40, 20)).toBe(2);
  });

  it('clamps to the full text length', () => {
    expect(typedLength(10, 10_000, 20)).toBe(10);
  });

  it('returns the full text length when msPerChar is non-positive', () => {
    expect(typedLength(10, 0, 0)).toBe(10);
  });
});

describe('typedLinesUpTo', () => {
  const lines = ['abc', 'de'];

  it('returns no lines at 0 visible chars', () => {
    expect(typedLinesUpTo(lines, 0)).toEqual([]);
  });

  it('types the first line before starting the second', () => {
    expect(typedLinesUpTo(lines, 2)).toEqual(['ab']);
  });

  it('the implicit newline consumes one budget slot after the first line finishes', () => {
    expect(typedLinesUpTo(lines, 4)).toEqual(['abc']);
  });

  it('starts the second line once the budget covers the first line + its newline', () => {
    expect(typedLinesUpTo(lines, 5)).toEqual(['abc', 'd']);
  });

  it('returns every line once the budget covers them all', () => {
    expect(typedLinesUpTo(lines, 100)).toEqual(['abc', 'de']);
  });

  it('returns an empty array for empty input', () => {
    expect(typedLinesUpTo([], 100)).toEqual([]);
  });
});

describe('countUp', () => {
  it('is 0 before any time has elapsed', () => {
    expect(countUp(312, 0, 900)).toBe(0);
  });

  it('is the target once the duration has elapsed', () => {
    expect(countUp(312, 900, 900)).toBe(312);
  });

  it('clamps past the duration', () => {
    expect(countUp(312, 5000, 900)).toBe(312);
  });

  it('interpolates linearly mid-way', () => {
    expect(countUp(200, 450, 900)).toBe(100);
  });

  it('returns the target outright for a non-positive duration', () => {
    expect(countUp(50, 10, 0)).toBe(50);
  });
});
