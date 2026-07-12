import { RadioGroup, RadioGroupItem } from '@/shared/ui/primitives/radio-group';
import { cn } from '@/shared/lib/utils';
import type { Runtime, RuntimeOption } from './database-types';
import { RUNTIME_META } from './database-types';
import { RUNTIME_ICON } from './runtime-badge';

export interface RuntimePickerProps {
  options: readonly RuntimeOption[];
  value: Runtime | undefined;
  onChange: (runtime: Runtime) => void;
}

/**
 * Runtime picker — ядро модели в UI. Показывает ВСЕ 4 рантайма (даже
 * недоступные/невалидные), у задизейбленных — причина. Так UI объясняет
 * placement (valid ∩ available), а не прячет варианты. Делегированный k8s
 * помечен пунктиром + меткой «delegated».
 */
export function RuntimePicker({ options, value, onChange }: RuntimePickerProps) {
  return (
    <RadioGroup
      value={value ?? ''}
      onValueChange={(v) => onChange(v as Runtime)}
      className="grid grid-cols-2 gap-2"
      data-od-id="runtime-picker"
    >
      {options.map((opt) => {
        const meta = RUNTIME_META[opt.runtime];
        const RuntimeIcon = RUNTIME_ICON[opt.runtime];
        const selected = value === opt.runtime;
        return (
          <label
            key={opt.runtime}
            data-disabled={!opt.enabled}
            data-selected={selected}
            className={cn(
              'flex items-start gap-2 rounded-lg border p-3 transition-colors',
              opt.enabled ? 'cursor-pointer' : 'pointer-events-none opacity-55',
              selected ? 'border-primary bg-primary/5' : 'border-border hover:border-border-2',
            )}
          >
            <RadioGroupItem value={opt.runtime} disabled={!opt.enabled} className="mt-0.5" />
            <span className="flex flex-1 flex-col gap-0.5">
              <span className="flex items-center gap-1.5 text-sm font-medium text-foreground">
                <RuntimeIcon className="size-4 text-muted-foreground" />
                {meta.label}
                {meta.class === 'delegated' && (
                  <span className="rounded bg-surface-3 px-1 text-[10px] font-medium tracking-[0.04em] text-muted-foreground uppercase">
                    delegated
                  </span>
                )}
              </span>
              <span className="text-xs text-muted-foreground">{meta.blurb}</span>
              <span className="text-[11px] text-muted-foreground">
                {opt.enabled ? `on: ${opt.nodes.join(', ')}` : opt.reason}
              </span>
            </span>
          </label>
        );
      })}
    </RadioGroup>
  );
}
