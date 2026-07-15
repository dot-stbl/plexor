import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogHeader,
  DialogTitle,
} from '@/shared/ui/primitives/dialog';
import { PreferencesDialog } from '@/shared/ui/primitives/preferences-dialog';
import { Button } from '@/shared/ui/primitives/button';
import { usePreferences, type Language, type Theme } from '@/shared/lib/preferences-provider';
import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Tune } from '@nine-thirty-five/material-symbols-react/rounded/700';
import { cn } from '@/lib/utils';

/**
 * Settings modal — opened from the sidebar's user menu.
 * Quick-access surface: theme + language right here, accent + fontSize
 * inside the full PreferencesDialog opened by the "Tune" button.
 */
export function AppSettingsDialog({
  open,
  onOpenChange,
}: {
  open: boolean;
  onOpenChange: (open: boolean) => void;
}) {
  const { t } = useTranslation();
  const { preferences, update } = usePreferences();
  const [prefsOpen, setPrefsOpen] = useState(false);

  const themeOptions: Array<{ value: Theme; labelKey: string }> = [
    { value: 'light', labelKey: 'preferences.themeLight' },
    { value: 'dark', labelKey: 'preferences.themeDark' },
    { value: 'system', labelKey: 'preferences.themeSystem' },
  ];

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

              {/* Theme — light/dark/system radio */}
              <div className="flex items-center justify-between rounded-lg border border-border p-3">
                <div className="min-w-0">
                  <div className="text-xs font-medium">{t('preferences.theme')}</div>
                  <div className="text-[11px] text-muted-foreground">
                    {t('preferences.themeSystem')} · {t('preferences.themeLight')} · {t('preferences.themeDark')}
                  </div>
                </div>
                <div role="radiogroup" className="flex gap-1">
                  {themeOptions.map((opt) => {
                    const isActive = preferences.theme === opt.value;
                    return (
                      <button
                        key={opt.value}
                        type="button"
                        role="radio"
                        aria-checked={isActive}
                        onClick={() => update('theme', opt.value)}
                        className={cn(
                          'min-w-[44px] rounded-md border px-2.5 py-1 text-xs transition-colors hover:border-foreground/30',
                          isActive
                            ? 'border-foreground/60 ring-1 ring-foreground/40 font-medium'
                            : 'border-border',
                        )}
                      >
                        {t(opt.labelKey)}
                      </button>
                    );
                  })}
                </div>
              </div>

              {/* Language — EN/RU radio, syncs i18n + persists via PreferencesProvider */}
              <div className="flex items-center justify-between rounded-lg border border-border p-3">
                <div className="min-w-0">
                  <div className="text-xs font-medium">{t('preferences.language')}</div>
                  <div className="text-[11px] text-muted-foreground">
                    {t('preferences.languageDescription')}
                  </div>
                </div>
                <div role="radiogroup" className="flex gap-1">
                  {([
                    { value: 'en' as Language, label: 'EN' },
                    { value: 'ru' as Language, label: 'RU' },
                  ]).map((l) => {
                    const isActive = preferences.language === l.value;
                    return (
                      <button
                        key={l.value}
                        type="button"
                        role="radio"
                        aria-checked={isActive}
                        onClick={() => update('language', l.value)}
                        className={cn(
                          'min-w-[44px] rounded-md border px-2.5 py-1 text-xs transition-colors hover:border-foreground/30',
                          isActive
                            ? 'border-foreground/60 ring-1 ring-foreground/40 font-medium'
                            : 'border-border',
                        )}
                      >
                        {l.label}
                      </button>
                    );
                  })}
                </div>
              </div>

              {/* Accent + fontSize — full tuning lives in PreferencesDialog */}
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
