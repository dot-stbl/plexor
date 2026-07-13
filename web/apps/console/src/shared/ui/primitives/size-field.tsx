import { useState } from 'react';
import { Stepper } from '@/shared/ui/primitives/stepper';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/shared/ui/primitives/select';
import type { SizeUnit } from '@/shared/ui/primitives/size';
import { cn } from '@/lib/utils';

/**
 * SizeField — точечный ввод размера с выбором единицы (эталon Proxmox: память
 * в МиБ, диск в ГиБ). Self-hosted → произвольные значения, точность до МиБ.
 * Наружу отдаёт **байты** (`onValueChange(bytes)`), внутри держит выбранную
 * единицу; переключение единицы сохраняет физический размер, меняя только
 * представление. Число вводится через `Stepper` (кламп на blur — можно набрать
 * любое промежуточное значение).
 *
 * @example
 *   <SizeField bytes={ramBytes} onValueChange={setRamBytes}
 *              units={['MiB','GiB']} min={SizeUtils.gibToBytes(1)} />
 */
const BASE: Record<SizeUnit, number> = {
  B: 1,
  KiB: 1024,
  MiB: 1024 ** 2,
  GiB: 1024 ** 3,
  TiB: 1024 ** 4,
  PiB: 1024 ** 5,
};

export interface SizeFieldProps {
  bytes: number;
  onValueChange: (bytes: number) => void;
  /** Доступные единицы (крупная→мелкая роли не играет). По умолчанию MiB/GiB/TiB. */
  units?: SizeUnit[];
  /** Границы в байтах. */
  min?: number;
  max?: number;
  /** Шаг в текущей единице (по умолчанию 1). */
  step?: number;
  id?: string;
  className?: string;
}

/** Крупнейшая единица, где значение целое и ≥ 1; иначе крупнейшая ≥ 1; иначе мельчайшая. */
function pickUnit(bytes: number, units: SizeUnit[]): SizeUnit {
  const byLargest = [...units].sort((a, b) => BASE[b] - BASE[a]);
  for (const u of byLargest) if (bytes >= BASE[u] && bytes % BASE[u] === 0) return u;
  for (const u of byLargest) if (bytes >= BASE[u]) return u;
  return byLargest[byLargest.length - 1];
}

export function SizeField({
  bytes,
  onValueChange,
  units = ['MiB', 'GiB', 'TiB'],
  min,
  max,
  step = 1,
  id,
  className,
}: SizeFieldProps) {
  const [unit, setUnit] = useState<SizeUnit>(() => pickUnit(bytes, units));

  const amount = bytes / BASE[unit];
  const minU = min !== undefined ? min / BASE[unit] : undefined;
  const maxU = max !== undefined ? max / BASE[unit] : undefined;

  return (
    <div className={cn('flex items-center gap-2', className)}>
      <Stepper
        id={id}
        value={amount}
        onValueChange={(n) => onValueChange(Math.round(n * BASE[unit]))}
        min={minU}
        max={maxU}
        step={step}
      />
      <Select
        items={units.map((u) => ({ value: u, label: u }))}
        value={unit}
        onValueChange={(v) => setUnit((v as SizeUnit) ?? unit)}
      >
        <SelectTrigger aria-label="Size unit" className="w-20">
          <SelectValue />
        </SelectTrigger>
        <SelectContent>
          {units.map((u) => (
            <SelectItem key={u} value={u}>
              {u}
            </SelectItem>
          ))}
        </SelectContent>
      </Select>
    </div>
  );
}
