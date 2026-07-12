import type { ReactNode } from 'react';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/shared/ui/primitives/select';

/**
 * SimpleSelect — тонкая обёртка над `Select` для частого случая
 * «массив строк-значений (+ опц. render лейбла)». Убирает бойлерплейт
 * Trigger/Content/Item в глубоких формах. Для сложных элементов
 * (иконки, бейджи в опциях) — используй `Select` напрямую.
 */
export interface SimpleSelectProps {
  id?: string;
  value: string;
  onChange: (value: string) => void;
  options: string[];
  /** Кастомный рендер лейбла опции (по умолчанию — само значение). */
  render?: (value: string) => ReactNode;
  placeholder?: string;
  className?: string;
}

export function SimpleSelect({
  id,
  value,
  onChange,
  options,
  render,
  placeholder,
  className,
}: SimpleSelectProps) {
  return (
    <Select
      items={options.map((o) => ({ value: o, label: o }))}
      value={value}
      onValueChange={(v) => onChange(v ?? value)}
    >
      <SelectTrigger id={id} className={className ?? 'w-full'}>
        <SelectValue placeholder={placeholder} />
      </SelectTrigger>
      <SelectContent>
        {options.map((o) => (
          <SelectItem key={o} value={o}>
            {render ? render(o) : o}
          </SelectItem>
        ))}
      </SelectContent>
    </Select>
  );
}
