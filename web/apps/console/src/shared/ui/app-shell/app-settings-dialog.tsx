import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogHeader,
  DialogTitle,
} from '@/shared/ui/primitives/dialog';
import { PreferencesDialog } from '@/shared/ui/primitives/preferences-dialog';
import { Button } from '@/shared/ui/primitives/button';
import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Tune } from '@nine-thirty-five/material-symbols-react/rounded/700';

/**
 * Settings modal — opened from the sidebar's user menu.
 * Thin launcher: the full visual settings (theme + accent + fontSize) live
 * in PreferencesDialog, opened from here. No duplicate theme row.
 */
export function AppSettingsDialog({
  open,
  onOpenChange,
}: {
  open: boolean;
  onOpenChange: (open: boolean) => void;
}) {
  const { t } = useTranslation();
  const [prefsOpen, setPrefsOpen] = useState(false);

  return (
    <>
      <Dialog open={open} onOpenChange={onOpenChange}>
        <DialogContent className="sm:max-w-md" data-od-id="settings-dialog">
          <DialogHeader>
            <DialogTitle className="text-sm">{t('preferences.settings.title')}</DialogTitle>
            <DialogDescription>{t('preferences.settings.description')}</DialogDescription>
          </DialogHeader>

          <div className="space-y-4">
            <section className="space-y-2" data-od-id="settings-appearance">
              <h3 className="text-[11px] font-medium tracking-[0.06em] text-muted-foreground uppercase">
                {t('preferences.settings.appearance')}
              </h3>
              <div className="flex items-center justify-between rounded-lg border border-border p-3">
                <div className="min-w-0">
                  <div className="text-xs font-medium">{t('preferences.settings.visualSettings')}</div>
                  <div className="text-[11px] text-muted-foreground">
                    {t('preferences.settings.visualSettingsDescription')}
                  </div>
                </div>
                <Button variant="outline" size="sm" onClick={() => setPrefsOpen(true)}>
                  <Tune className="size-3.5" />
                  {t('common.configure')}
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
