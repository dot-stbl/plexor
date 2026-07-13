import { useMemo, type ComponentProps } from 'react';
import { cn } from '@/lib/utils';

/**
 * Human-readable byte size. Plexor self-hosted allocates RAM and disk in
 * binary units (1024-based), so KiB/MiB/GiB/TiB are the working set;
 * the unit is auto-selected from the magnitude of the value.
 *
 * Data shape: most API contracts return bytes (or KB for legacy). The
 * component does the unit math — screens just pass the raw number.
 *
 * @example
 *   <Size bytes={vm.diskBytes} />            // auto: 80.0 GiB
 *   <Size bytes={512} decimals={0} />        // 512 B
 *   <Size bytes={4 * GiB} unit="GiB" />      // force GiB even for tiny values
 */
export type SizeUnit = 'B' | 'KiB' | 'MiB' | 'GiB' | 'TiB' | 'PiB';

const BINARY_UNITS: readonly SizeUnit[] = ['B', 'KiB', 'MiB', 'GiB', 'TiB', 'PiB'];

const UNIT_BASE: Record<SizeUnit, number> = {
  B: 1,
  KiB: 1024,
  MiB: 1024 ** 2,
  GiB: 1024 ** 3,
  TiB: 1024 ** 4,
  PiB: 1024 ** 5,
};

export interface SizeProps extends Omit<ComponentProps<'span'>, 'children'> {
  /** Size in bytes. The unit is auto-selected from this value. */
  bytes: number;
  /** Decimal places. Default 1 (so 1.5 GiB, not 2 GiB). */
  decimals?: number;
  /**
   * Force a specific unit. Bypasses auto-selection. Use when the screen
   * has a fixed unit context (e.g. "show all RAM in GiB" for a
   * cluster's node list).
   */
  unit?: SizeUnit;
  /** Append the unit suffix to the value. Default true. */
  showUnit?: boolean;
  /** Mute the value text (for use as a secondary number in a row). */
  muted?: boolean;
}

/**
 * Pick the largest unit where the displayed value stays >= 1, for sane
 * readouts. 0 bytes stays at 0 B (we don't render "0.0 GiB").
 */
function pickUnit(bytes: number, decimals: number): { value: number; unit: SizeUnit } {
  if (bytes === 0) return { value: 0, unit: 'B' };
  let value = bytes;
  let unit: SizeUnit = 'B';
  for (const candidate of BINARY_UNITS) {
    if (value < UNIT_BASE[candidate] || candidate === 'PiB') {
      unit = candidate;
      break;
    }
    value = value / UNIT_BASE[candidate];
    unit = candidate;
  }
  // One extra decimal below 1 so 0.5 GiB doesn't round to "0 GiB".
  if (value > 0 && value < 1 && decimals >= 0) {
    return { value: bytes, unit: 'B' };
  }
  return { value, unit };
}

export function Size({
  bytes,
  decimals = 1,
  unit,
  showUnit = true,
  muted = false,
  className,
  ...props
}: SizeProps) {
  const { value, unit: pickedUnit } = useMemo(() => {
    if (unit) {
      const raw = bytes / UNIT_BASE[unit];
      return { value: raw, unit };
    }
    return pickUnit(bytes, decimals);
  }, [bytes, decimals, unit]);

  // Whole numbers render without trailing zeros ("4 GiB" not "4.0 GiB").
  const isWhole = Number.isFinite(value) && Math.abs(value - Math.round(value)) < 1e-9;
  const displayDecimals = isWhole ? 0 : decimals;
  const formatted = value.toFixed(displayDecimals);

  return (
    <span
      data-slot="size"
      data-unit={pickedUnit}
      className={cn(
        'inline-block align-baseline font-mono tracking-tight tabular-nums leading-none',
        muted && 'text-muted-foreground',
        className,
      )}
      {...props}
    >
      {formatted}
      {showUnit && (
        <span
          aria-hidden
          className="ml-0.5 text-[0.85em] text-muted-foreground"
        >
          {pickedUnit}
        </span>
      )}
    </span>
  );
}

/** Sugar for callers that already have a value in some unit. */
export const SizeUtils = {
  /** Convert GB (decimal) to bytes — useful for legacy mock data. */
  gbToBytes: (gb: number) => Math.round(gb * 1_000_000_000),
  /** Convert GiB (binary) to bytes — the self-hosted convention. */
  gibToBytes: (gib: number) => Math.round(gib * 1024 ** 3),
} as const;