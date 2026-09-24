import { afterEach, describe, expect, it, vi } from 'vitest';
import { createVm, deleteVm, getVm, listVms, resetStore, startVm, stopVm } from './store';

afterEach(() => {
  vi.useRealTimers();
  resetStore();
});

describe('mock VM store', () => {
  it('seeds from the fixture fleet', () => {
    expect(listVms().length).toBeGreaterThan(0);
  });

  it('creates a VM in "provisioning", then flips to "running" once the window elapses', () => {
    vi.useFakeTimers();
    const created = createVm(
      { name: 'test-vm', machineType: '2-4', diskGb: 20, subnetId: 'subnet-prod-eu-compute-a', image: 'img-ubuntu-2404' },
      'user-dev-1',
    );
    expect(created.status).toBe('provisioning');
    expect(getVm(created.id)?.status).toBe('provisioning');
    vi.advanceTimersByTime(10_000);
    expect(getVm(created.id)?.status).toBe('running');
  });

  it('start/stop/delete mutate the record', () => {
    const created = createVm({ name: 'lifecycle-vm', machineType: '2-4', diskGb: 20 }, 'user-dev-1');
    expect(startVm(created.id, 'user-dev-1')?.status).toBe('running');
    expect(stopVm(created.id, 'user-dev-1')?.status).toBe('stopped');
    expect(deleteVm(created.id, 'user-dev-1')).toBe(true);
    expect(getVm(created.id)).toBeUndefined();
  });

  it('start/stop/delete on an unknown id are no-ops that report failure', () => {
    expect(startVm('nope', 'user-dev-1')).toBeUndefined();
    expect(stopVm('nope', 'user-dev-1')).toBeUndefined();
    expect(deleteVm('nope', 'user-dev-1')).toBe(false);
  });

  it('resetStore reseeds from the fixtures, discarding prior mutations', () => {
    const before = listVms().length;
    createVm({ name: 'temp-vm', machineType: '2-4', diskGb: 20 }, 'user-dev-1');
    expect(listVms().length).toBe(before + 1);
    resetStore();
    expect(listVms().length).toBe(before);
  });
});