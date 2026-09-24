import { describe, expect, it } from 'vitest';
import { BENTO_CELLS } from '../bento/bento-data';
import { SERVICE_CONTENT, requireServiceContent } from './service-content';

describe('SERVICE_CONTENT', () => {
  it('has the same key set as BENTO_CELLS', () => {
    const bentoIds = BENTO_CELLS.map((cell) => cell.id).sort();
    const serviceIds = Object.keys(SERVICE_CONTENT).sort();
    expect(serviceIds).toEqual(bentoIds);
  });

  it('has at least one entry per BENTO_CELLS id', () => {
    for (const cell of BENTO_CELLS) {
      expect(SERVICE_CONTENT[cell.id]).toBeDefined();
    }
  });

  it('has no extra keys beyond BENTO_CELLS', () => {
    const bentoIds = new Set(BENTO_CELLS.map((cell) => cell.id));
    for (const id of Object.keys(SERVICE_CONTENT)) {
      expect(bentoIds.has(id)).toBe(true);
    }
  });

  it('every entry has a non-empty lead and a docsHref starting with /docs', () => {
    for (const [id, entry] of Object.entries(SERVICE_CONTENT)) {
      expect(entry.lead.length, `lead for ${id}`).toBeGreaterThan(0);
      expect(entry.docsHref, `docsHref for ${id}`).toMatch(/^\/docs/);
    }
  });

  it('every entry has at least one capability', () => {
    for (const [id, entry] of Object.entries(SERVICE_CONTENT)) {
      expect(entry.capabilities.length, `capabilities for ${id}`).toBeGreaterThan(0);
    }
  });

  it('every entry with bento status next has every capability marked planned', () => {
    for (const cell of BENTO_CELLS) {
      if (cell.status !== 'next') continue;
      const entry = SERVICE_CONTENT[cell.id];
      expect(entry, `SERVICE_CONTENT[${cell.id}]`).toBeDefined();
      for (const capability of entry.capabilities) {
        expect(capability.planned, `${cell.id}.${capability.title}`).toBe(true);
      }
    }
  });
});

describe('requireServiceContent', () => {
  it('returns the matching entry for a known id', () => {
    const content = requireServiceContent('compute');
    expect(content.lead.length).toBeGreaterThan(0);
  });

  it('throws for an unknown id', () => {
    expect(() => requireServiceContent('not-a-real-service')).toThrow(
      /No SERVICE_CONTENT entry for id/,
    );
  });
});
