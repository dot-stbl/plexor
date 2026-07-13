import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogHeader,
  DialogTitle,
} from '@/shared/ui/primitives/dialog';
import { ModeToggle } from '@/shared/ui/primitives/theme-toggle';
import { PreferencesDialog } from '@/shared/ui/primitives/preferences-dialog';
import { Button } from '@/shared/ui/primitives/button';
import { useState } from 'react';
import { SlidersHorizontal } from '@/shared/ui/icon';

/**
 * Settings modal — opened from the sidebar's user menu.
 * Houses a "Тема" row with a quick theme switch, and a button that
 * opens the full PreferencesDialog (accent + font + reset).
 */
export function AppSettingsDialog({
  open,
  onOpenChange,
}: {
  open: boolean;
  onOpenChange: (open: boolean) => void;
}) {
  const [prefsOpen, setPrefsOpen] = useState(false);

  return (
    <>
      <Dialog open={open} onOpenChange={onOpenChange}>
        <DialogContent className="sm:max-w-md" data-od-id="settings-dialog">
          <DialogHeader>
            <DialogTitle className="text-sm">Настройки</DialogTitle>
            <DialogDescription>Оформление и параметры аккаунта</DialogDescription>
          </DialogHeader>

          <div className="space-y-4">
            <section className="space-y-2" data-od-id="settings-appearance">
              <h3 className="text-[11px] font-medium tracking-[0.06em] text-muted-foreground uppercase">
                Оформление
              </h3>
              <div className="flex items-center justify-between rounded-lg border border-border p-3">
                <div className="min-w-0">
                  <div className="text-xs font-medium">Тема</div>
                  <div className="text-[11px] text-muted-foreground">
                    Светлая, тёмная или системная
                  </div>
                </div>
                <ModeToggle />
              </div>
              <div className="flex items-center justify-between rounded-lg border border-border p-3">
                <div className="min-w-0">
                  <div className="text-xs font-medium">Акцент и размер текста</div>
                  <div className="text-[11px] text-muted-foreground">
                    Цвет ссылок, кнопок, масштаб всей UI
                  </div>
                </div>
                <Button variant="outline" size="sm" onClick={() => setPrefsOpen(true)}>
                  <SlidersHorizontal className="size-3.5" />
                  Настроить
                </Button>
              </div>
            </section>
          </div>
        </DialogContent>
      </Dialog>

      <PreferencesDialog open={prefsOpen} onOpenChange={setPrefsOpen} />
    </>
  );
}
