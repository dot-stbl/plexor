import { afterEach, describe, expect, it } from 'vitest';
import { appendAuditEntry, makeAuditEntries, queryAuditEntries, resetAuditLog } from './audit';

afterEach(() => {
  resetAuditLog();
});

describe('audit log', () => {
  it('makeAuditEntries returns deterministic historical rows', () => {
    const a = makeAuditEntries(5);
    const b = makeAuditEntries(5);
    expect(a).toEqual(b);
    expect(a).toHaveLength(5);
  });

  it('appendAuditEntry prepends a live row that queryAuditEntries returns first', () => {
    appendAuditEntry({ action: 'vm.create', actorUserId: 'user-dev-1', targetKind: 'vm', targetId: 'vm-test-1' });
    const rows = queryAuditEntries({ limit: 1 });
    expect(rows[0]?.action).toBe('vm.create');
    expect(rows[0]?.targetId).toBe('vm-test-1');
  });

  it('filters by action', () => {
    appendAuditEntry({ action: 'vm.delete', actorUserId: 'user-dev-1', targetKind: 'vm', targetId: 'vm-test-2' });
    const rows = queryAuditEntries({ action: 'vm.delete' });
    expect(rows.length).toBeGreaterThan(0);
    expect(rows.every((r) => r.action === 'vm.delete')).toBe(true);
  });

  it('respects limit', () => {
    const rows = queryAuditEntries({ limit: 3 });
    expect(rows.length).toBeLessThanOrEqual(3);
  });

  it('resetAuditLog clears live rows but not the historical seed', () => {
    appendAuditEntry({ action: 'vm.stop', actorUserId: 'user-dev-1', targetKind: 'vm', targetId: 'vm-test-3' });
    resetAuditLog();
    const rows = queryAuditEntries({ action: 'vm.stop', actorUserId: 'user-dev-1' });
    expect(rows.find((r) => r.targetId === 'vm-test-3')).toBeUndefined();
    expect(makeAuditEntries(1)).toHaveLength(1);
  });
});