import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/shared/ui/primitives/select';
import { Input } from '@/shared/ui/primitives/input';
import { MagnifyingGlass } from '@phosphor-icons/react';
import type { VmFilters } from './filter-vms';

export type { VmFilters };

interface VmFiltersBarProps {
  value: VmFilters;
  onChange: (next: VmFilters) => void;
  zones: readonly string[];
}

const STATUS_OPTIONS: { value: VmFilters['status']; label: string }[] = [
  { value: 'all', label: 'Все статусы' },
  { value: 'running', label: 'Running' },
  { value: 'stopped', label: 'Stopped' },
  { value: 'error', label: 'Error' },
  { value: 'provisioning', label: 'Provisioning' },
];

/**
 * Controlled filter bar. Debounce is the parent's job — this component
 * fires onChange synchronously on every keystroke.
 */
export function VmFiltersBar({ value, onChange, zones }: VmFiltersBarProps) {
  const setStatus = (status: VmFilters['status']) => onChange({ ...value, status });
  const setZone = (zone: VmFilters['zone']) => onChange({ ...value, zone });

  return (
    <div
      data-od-id="vms-filters"
      className="flex flex-wrap items-center gap-2 pb-3"
    >
      <Select
        items={STATUS_OPTIONS}
        value={value.status}
        onValueChange={(v) => setStatus(v as VmFilters['status'])}
      >
        <SelectTrigger size="sm" className="min-w-[140px]">
          <SelectValue />
        </SelectTrigger>
        <SelectContent>
          {STATUS_OPTIONS.map((opt) => (
            <SelectItem key={opt.value} value={opt.value}>
              {opt.label}
            </SelectItem>
          ))}
        </SelectContent>
      </Select>

      <Select
        items={[{ value: 'all' as const, label: 'Все зоны' }, ...zones.map((z) => ({ value: z, label: z }))]}
        value={value.zone}
        onValueChange={(v) => setZone(v as VmFilters['zone'])}
      >
        <SelectTrigger size="sm" className="min-w-[140px]">
          <SelectValue />
        </SelectTrigger>
        <SelectContent>
          <SelectItem value="all">Все зоны</SelectItem>
          {zones.map((zone) => (
            <SelectItem key={zone} value={zone}>
              {zone}
            </SelectItem>
          ))}
        </SelectContent>
      </Select>

      <div className="relative min-w-[220px] flex-1">
        <MagnifyingGlass className="pointer-events-none absolute top-1/2 left-2 size-3.5 -translate-y-1/2 text-muted-foreground" />
        <Input
          value={value.search}
          onChange={(event) => onChange({ ...value, search: event.target.value })}
          placeholder="Поиск по имени, IP или ID"
          className="h-7 pl-7 text-xs"
        />
      </div>
    </div>
  );
}

