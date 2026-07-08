import type { Vm, VmStatus } from '@/shared/api';

/**
 * Filter state for the VM list. `'all'` means "no filter for this axis".
 */
export interface VmFilters {
  status: 'all' | VmStatus;
  zone: 'all' | string;
  search: string;
}

export const VM_FILTERS_DEFAULT: VmFilters = {
  status: 'all',
  zone: 'all',
  search: '',
};

/**
 * Pure filter pass over the VM list. Search matches name, internalIp and id
 * (lowercased substring). Caller controls debouncing — this function is cheap.
 */
export function filterVms(items: readonly Vm[], filters: VmFilters): Vm[] {
  const q = filters.search.trim().toLowerCase();
  return items.filter((vm) => {
    if (filters.status !== 'all' && vm.status !== filters.status) return false;
    if (filters.zone !== 'all' && vm.zone !== filters.zone) return false;
    if (!q) return true;
    return (
      vm.name.toLowerCase().includes(q) ||
      vm.internalIp.includes(q) ||
      vm.id.toLowerCase().includes(q)
    );
  });
}

export function uniqueZones(items: readonly Vm[]): string[] {
  return [...new Set(items.map((v) => v.zone))].sort();
}

/** Total / running live-counter (label component feeds this). */
export function summarizeStatus(items: readonly Vm[]): { total: number; running: number } {
  let running = 0;
  for (const vm of items) if (vm.status === 'running') running += 1;
  return { total: items.length, running };
}