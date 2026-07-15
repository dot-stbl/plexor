import { useState, type ReactNode } from 'react';
import { Add, Remove } from '@nine-thirty-five/material-symbols-react/rounded/700';
import { Button } from '@/shared/ui/primitives/button';
import { Input } from '@/shared/ui/primitives/input';
import { cn } from '@/lib/utils';

/**
 * Stepper — числовой ввод с `−`/`+` (эталон YC: кол-во хостов, размер диска).
 * min/max/step + опц. suffix. Значение остаётся числом (не строкой).
 *
 * Свободный ввод: пока пользователь печатает, значение держится в локальном
 * «драфте» и НЕ клампится на каждой клавише (иначе «128» при min=8 не набрать
 * — «1» тут же схлопнулось бы в 8). Кламп и коммит — на blur/Enter; кнопки
 * `−`/`+` работают от текущего эффективного значения.
 */
export interface StepperProps {
  value: number;
  onValueChange: (value: number) => void;
  min?: number;
  max?: number;
  step?: number;
  suffix?: ReactNode;
  id?: string;
  className?: string;
}

export function Stepper({
  value,
  onValueChange,
  min = 0,
  max = Number.MAX_SAFE_INTEGER,
  step = 1,
  suffix,
  id,
  className,
}: StepperProps) {
  const clamp = (n: number) => Math.min(max, Math.max(min, n));
  const [draft, setDraft] = useState<string | null>(null);
  const shown = draft ?? String(value);
  const current =
    draft !== null && draft.trim() !== '' && Number.isFinite(Number(draft)) ? Number(draft) : value;

  const commit = () => {
    if (draft === null) return;
    const n = Number(draft);
    onValueChange(draft.trim() !== '' && Number.isFinite(n) ? clamp(n) : value);
    setDraft(null);
  };

  const bump = (delta: number) => {
    onValueChange(clamp(current + delta));
    setDraft(null);
  };

  return (
    <div className={cn('inline-flex items-center gap-1', className)}>
      <Button
        type="button"
        variant="outline"
        size="icon-sm"
        aria-label="Decrease"
        disabled={current <= min}
        onClick={() => bump(-step)}
      >
        <Remove className="size-3.5" />
      </Button>
      <div className="relative">
        <Input
          id={id}
          type="number"
          inputMode="numeric"
          value={shown}
          min={min}
          max={max === Number.MAX_SAFE_INTEGER ? undefined : max}
          step={step}
          onChange={(e) => setDraft(e.target.value)}
          onBlur={commit}
          onKeyDown={(e) => {
            if (e.key === 'Enter') {
              e.preventDefault();
              commit();
            }
          }}
          className={cn('h-7 w-24 text-center tabular-nums', suffix && 'pr-9')}
        />
        {suffix && (
          <span className="pointer-events-none absolute inset-y-0 right-2 flex items-center text-xs text-muted-foreground">
            {suffix}
          </span>
        )}
      </div>
      <Button
        type="button"
        variant="outline"
        size="icon-sm"
        aria-label="Increase"
        disabled={current >= max}
        onClick={() => bump(step)}
      >
        <Add className="size-3.5" />
      </Button>
    </div>
  );
}
