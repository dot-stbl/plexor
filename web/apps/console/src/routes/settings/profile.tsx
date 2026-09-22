import { createFileRoute, useNavigate } from '@tanstack/react-router';
import { useTranslation } from 'react-i18next';
import { useState } from 'react';
import { ArrowBack, DarkMode, LightMode, Logout, Tune } from '@nine-thirty-five/material-symbols-react/rounded/700';
import { Button } from '@/shared/ui/primitives/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/shared/ui/primitives/card';
import { StatusPill } from '@/shared/ui/primitives/status-pill';
import { PageTemplate } from '@/shared/ui/app-shell';
import { PreferencesDialog } from '@/shared/ui/primitives/preferences-dialog';
import { cn } from '@/lib/utils';
import { routeHead } from '@/shared/lib/route-head';
import {
  usePreferences,
  type Theme,
} from '@/shared/lib/preferences-provider';
import {
  clearSession,
  readSession,
} from '@/features/auth/session-storage';

/**
 * Settings → Profile route (`/settings/profile`).
 *
 * Replaces the prior `app-settings-dialog` modal. The sidebar's user
 * menu now navigates here instead of opening a dialog. The page is the
 * single place where the user reviews their profile, picks a theme,
 * and signs out — language/security/API keys land in adjacent routes
 * (Phase 2+).
 *
 * Theme controls write through the same `PreferencesProvider` the
 * dialog used — the per-user localStorage key (`plexor-preferences::<id>`)
 * means each signed-in user keeps their own theme.
 *
 * Sign-out: `clearSession()` + `navigate({ to: '/login' })`. The router
 * guards on `/login` are the `beforeLoad` redirects on protected routes;
 * clearing the session + navigating is enough.
 */
export const Route = createFileRoute('/settings/profile')({
  component: SettingsProfilePage,
  ...routeHead('Settings'),
});

const THEME_OPTIONS: Array<{ value: Theme; labelKey: string; Icon: typeof LightMode }> = [
  { value: 'light', labelKey: 'settings.theme.light', Icon: LightMode },
  { value: 'dark', labelKey: 'settings.theme.dark', Icon: DarkMode },
  { value: 'system', labelKey: 'settings.theme.system', Icon: LightMode },
];

function SettingsProfilePage() {
  const { t } = useTranslation();
  const navigate = useNavigate();
  const { preferences, update, reset } = usePreferences();
  const [advancedOpen, setAdvancedOpen] = useState(false);

  const session = readSession();
  const displayName = session?.user.displayName ?? t('shell.user.name');
  const email = session?.user.email ?? t('shell.user.email');

  const handleSignOut = () => {
    clearSession();
    void navigate({ to: '/login' });
  };

  return (
    <PageTemplate
      title={t('settings.title')}
      description={t('settings.subtitle')}
      width="narrow"
      data-od-id="settings-profile"
      actions={
        <Button
          variant="outline"
          size="sm"
          onClick={() => {
            void navigate({ to: '/' });
          }}
        >
          <ArrowBack className="size-4" />
          {t('common.back')}
        </Button>
      }
    >
      <div className="space-y-4">
        {/* Profile — read-only identity from the session */}
        <Card data-od-id="settings-profile-card">
          <CardHeader>
            <CardTitle>{t('settings.profile.heading')}</CardTitle>
            <CardDescription>
              {session ? session.user.id : t('shell.user.unknown')}
            </CardDescription>
          </CardHeader>
          <CardContent className="space-y-3">
            <Row label={t('settings.profile.displayName')} value={displayName} />
            <Row label={t('settings.profile.email')} value={email} mono />
            <div className="flex justify-end pt-2">
              <Button variant="outline" size="sm" onClick={handleSignOut}>
                <Logout className="size-4" />
                {t('settings.profile.signOut')}
              </Button>
            </div>
          </CardContent>
        </Card>

        {/* Theme — light / dark / system radio. Writes through
            PreferencesProvider so the per-user localStorage key is
            updated. The advanced dialog (accent + fontSize) opens via
            the Tune button to keep this surface focused. */}
        <Card data-od-id="settings-theme-card">
          <CardHeader>
            <CardTitle>{t('settings.theme.heading')}</CardTitle>
            <CardDescription>
              {preferences.accent} — {preferences.fontSize}
            </CardDescription>
          </CardHeader>
          <CardContent className="space-y-3">
            <div role="radiogroup" className="grid grid-cols-3 gap-2">
              {THEME_OPTIONS.map((option) => {
                const Icon = option.Icon;
                const isActive = preferences.theme === option.value;
                return (
                  <button
                    key={option.value}
                    type="button"
                    role="radio"
                    aria-checked={isActive}
                    onClick={() => {
                      update('theme', option.value);
                    }}
                    className={cn(
                      'flex flex-col items-center gap-1.5 rounded-md border bg-background p-3 text-xs transition-colors hover:border-foreground/30',
                      isActive
                        ? 'border-foreground/60 ring-1 ring-foreground/40'
                        : 'border-border',
                    )}
                  >
                    <Icon className="size-4" />
                    <span>{t(option.labelKey)}</span>
                  </button>
                );
              })}
            </div>
            <div className="flex items-center justify-between rounded-lg border border-border p-3">
              <div className="min-w-0">
                <div className="text-xs font-medium">{t('preferences.accent')} — {t('preferences.fontSize')}</div>
                <div className="text-[11px] text-muted-foreground">
                  {t('preferences.fontSizeDescription')}
                </div>
              </div>
              <Button variant="outline" size="sm" onClick={() => setAdvancedOpen(true)}>
                <Tune className="size-3.5" />
                {t('common.configure')}
              </Button>
            </div>
            <div className="flex justify-end">
              <Button variant="ghost" size="sm" onClick={reset}>
                {t('preferences.reset')}
              </Button>
            </div>
          </CardContent>
        </Card>

        {/* Language — placeholder; i18next language picker lives in the
            sidebar / nav for now. Lands as a dedicated picker in Phase
            2+ when the locale list grows beyond EN + RU. */}
        <Card data-od-id="settings-language-card">
          <CardHeader>
            <CardTitle>{t('settings.language.heading')}</CardTitle>
          </CardHeader>
          <CardContent className="flex items-center justify-between">
            <span className="text-xs text-muted-foreground">{t('settings.language.comingSoon')}</span>
            <StatusPill variant="idle" hideDot className="px-1.5 py-0 text-[9.5px] font-normal">
              {t('settings.language.comingSoon')}
            </StatusPill>
          </CardContent>
        </Card>

        {/* Security — placeholder for MFA / SSH keys / API tokens. */}
        <Card data-od-id="settings-security-card">
          <CardHeader>
            <CardTitle>{t('settings.security.heading')}</CardTitle>
          </CardHeader>
          <CardContent className="flex items-center justify-between">
            <span className="text-xs text-muted-foreground">{t('settings.security.comingSoon')}</span>
            <StatusPill variant="idle" hideDot className="px-1.5 py-0 text-[9.5px] font-normal">
              {t('settings.security.comingSoon')}
            </StatusPill>
          </CardContent>
        </Card>
      </div>

      <PreferencesDialog open={advancedOpen} onOpenChange={setAdvancedOpen} />
    </PageTemplate>
  );
}

interface RowProps {
  label: string;
  value: string;
  mono?: boolean;
}

function Row({ label, value, mono = false }: RowProps) {
  return (
    <div className="flex items-center justify-between gap-3 border-b border-border/60 pb-2 last:border-0">
      <span className="text-[11px] font-medium tracking-[0.06em] text-muted-foreground uppercase">
        {label}
      </span>
      <span className={cn('truncate text-xs', mono && 'font-mono text-[11px]')}>{value}</span>
    </div>
  );
}
