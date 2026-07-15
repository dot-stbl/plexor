import { useState } from 'react';
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/shared/ui/primitives/dialog';
import { Button } from '@/shared/ui/primitives/button';
import { Field, FieldDescription, FieldGroup, FieldLabel } from '@/shared/ui/primitives/field';
import { DarkMode, LightMode, Refresh } from '@nine-thirty-five/material-symbols-react/rounded/700';
import { cn } from '@/lib/utils';
import { usePreferences, type Accent, type FontSize, type Theme } from '@/shared/lib/preferences-provider';

interface PreferencesDialogProps {
  open: boolean;
  onOpenChange: (open: boolean) => void;
}

const ACCENTS: Array<{ value: Accent; label: string; preview: string }> = [
  { value: 'plexor', label: 'Plexor',      preview: 'oklch(28% 0.02 255)' },
  { value: 'blue',   label: 'Синий',       preview: 'oklch(55% 0.18 252)' },
  { value: 'green',  label: 'Зелёный',     preview: 'oklch(58% 0.15 155)' },
  { value: 'orange', label: 'Оранжевый',   preview: 'oklch(68% 0.16 50)' },
  { value: 'pink',   label: 'Розовый',     preview: 'oklch(64% 0.18 0)' },
];

const FONT_SIZES: Array<{ value: FontSize; label: string }> = [
  { value: 'small',  label: 'Маленький' },
  { value: 'medium', label: 'Обычный' },
  { value: 'large',  label: 'Большой' },
];

const THEMES: Array<{ value: Theme; label: string; Icon: typeof LightMode }> = [
  { value: 'light',  label: 'Светлая',  Icon: LightMode },
  { value: 'dark',   label: 'Тёмная',   Icon: DarkMode },
  { value: 'system', label: 'Системная', Icon: LightMode }, // Material Symbols — no half-circle; LightMode icon re-used
];

/**
 * User visual preferences. Three sections:
 *   1. Theme — light / dark / system
 *   2. Accent color — swatch picker (5 presets, CSS var override)
 *   3. Font size — small / medium / large (rem-based scale on <html>)
 *
 * A "Reset" button restores all three to PREFERENCES_DEFAULT. Every
 * change is persisted to localStorage and applied to the document
 * immediately by PreferencesProvider.
 */
export function PreferencesDialog({ open, onOpenChange }: PreferencesDialogProps) {
  const { preferences, update, reset } = usePreferences();
  // Force a re-render of swatch previews when document.documentElement
  // style changes (i.e. when the accent var updates). Cheap re-render.
  const [, setPreviewTick] = useState(0);

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="sm:max-w-md">
        <DialogHeader>
          <DialogTitle className="text-sm">Визуальные настройки</DialogTitle>
          <DialogDescription>
            Тема, акцентный цвет и размер текста. Сохраняются в этом браузере.
          </DialogDescription>
        </DialogHeader>

        <FieldGroup>
          <Field>
            <FieldLabel>Тема</FieldLabel>
            <div role="radiogroup" className="grid grid-cols-3 gap-2">
              {THEMES.map((t) => {
                const isActive = preferences.theme === t.value;
                const Icon = t.Icon;
                return (
                  <button
                    key={t.value}
                    type="button"
                    role="radio"
                    aria-checked={isActive}
                    onClick={() => update('theme', t.value)}
                    className={cn(
                      'flex flex-col items-center gap-1.5 rounded-md border bg-background p-2.5 text-xs transition-colors hover:border-foreground/30',
                      isActive
                        ? 'border-foreground/60 ring-1 ring-foreground/40'
                        : 'border-border',
                    )}
                  >
                    <Icon className="size-4" />
                    <span>{t.label}</span>
                  </button>
                );
              })}
            </div>
          </Field>

          <Field>
            <FieldLabel>Акцентный цвет</FieldLabel>
            <div className="grid grid-cols-5 gap-2">
              {ACCENTS.map((a) => {
                const isActive = preferences.accent === a.value;
                return (
                  <button
                    key={a.value}
                    type="button"
                    onClick={() => {
                      update('accent', a.value);
                      setPreviewTick((t) => t + 1);
                    }}
                    aria-pressed={isActive}
                    title={a.label}
                    className={cn(
                      'group flex flex-col items-center gap-1.5 rounded-md border p-1.5 transition-colors',
                      isActive
                        ? 'border-foreground/60 ring-1 ring-foreground/40'
                        : 'border-border hover:border-foreground/30',
                    )}
                  >
                    <span
                      aria-hidden
                      className="block size-6 rounded-full border border-foreground/10"
                      style={{ background: a.preview }}
                    />
                    <span className="text-[10px] text-muted-foreground">{a.label}</span>
                  </button>
                );
              })}
            </div>
            <FieldDescription>Применяется ко всем ссылкам, кнопкам, выделениям и focus-рингам.</FieldDescription>
          </Field>

          <Field>
            <FieldLabel>Размер текста</FieldLabel>
            <div role="radiogroup" className="grid grid-cols-3 gap-2">
              {FONT_SIZES.map((f) => {
                const isActive = preferences.fontSize === f.value;
                return (
                  <button
                    key={f.value}
                    type="button"
                    role="radio"
                    aria-checked={isActive}
                    onClick={() => update('fontSize', f.value)}
                    className={cn(
                      'rounded-md border bg-background p-2.5 transition-colors hover:border-foreground/30',
                      isActive
                        ? 'border-foreground/60 ring-1 ring-foreground/40'
                        : 'border-border',
                    )}
                  >
                    <span className={f.value === 'small' ? 'text-xs' : f.value === 'large' ? 'text-base font-medium' : 'text-sm'}>
                      Aa
                    </span>
                    <span className="mt-1 block text-[10px] text-muted-foreground">{f.label}</span>
                  </button>
                );
              })}
            </div>
            <FieldDescription>Масштабирует всю UI пропорционально.</FieldDescription>
          </Field>
        </FieldGroup>

        <DialogFooter>
          <Button
            variant="ghost"
            size="sm"
            onClick={() => {
              reset();
              setPreviewTick((t) => t + 1);
            }}
          >
            <Refresh className="size-3.5" />
            Сбросить
          </Button>
          <Button onClick={() => onOpenChange(false)}>Готово</Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
