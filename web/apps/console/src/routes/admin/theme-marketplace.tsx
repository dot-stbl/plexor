import { createFileRoute, Link, useNavigate } from '@tanstack/react-router';
import { useEffect, useMemo, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Palette, Storefront } from '@nine-thirty-five/material-symbols-react/rounded/700';
import { PageTemplate } from '@/shared/ui/app-shell';
import { Button } from '@/shared/ui/primitives/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/shared/ui/primitives/card';
import { Badge } from '@/shared/ui/primitives/badge';
import { EmptyState } from '@/shared/ui/primitives/empty-state';
import { cn } from '@/shared/lib/utils';
import { routeHead } from '@/shared/lib/route-head';
import { applyPreset } from '@/shared/lib/themes/apply-tokens';
import {
  getPreset,
  listPresets,
  type ThemePreset,
} from '@/shared/lib/themes';
import {
  useCommunityThemes,
  useActivateTheme,
  useDeactivateTheme,
  useActiveThemeId,
} from '@/features/themes/use-community-themes';

/**
 * AdminThemeMarketplacePage — operator-only console surface for
 * browsing + activating community themes. Phase 5+ persists
 * activation in a per-org backend row
 * (`branding.theme_installations`) via the kubb-generated
 * `useUpdateBrandingTheme` mutation; the next page load reads
 * the choice back through `useGetBrandingTheme()`.
 *
 * Page sections:
 *   1. Community themes — the marketplace feed. Each card
 *      shows the theme name, author, version, preview
 *      swatches, and an Activate button. The currently-active
 *      theme (read from the backend on first paint) is
 *      highlighted.
 *   2. Built-in presets — the same 3 the picker UI uses today
 *      (`plexor-default-light`, `plexor-default-dark`,
 *      `plexor-noir`). Listed for context, not editable here.
 *   3. Reset button — calls `useDeactivateTheme` (DELETE on
 *      the same path) to fall back to operator defaults.
 */

export const Route = createFileRoute('/admin/theme-marketplace')({
  component: AdminThemeMarketplacePage,
  ...routeHead('Theme Marketplace'),
});

function activateBuiltIn(preset: ThemePreset) {
  applyPreset(preset);
  if (typeof document !== 'undefined') {
    document.documentElement.dataset.theme = preset.id;
    document.documentElement.classList.toggle('dark', preset.isDarkPreferred);
  }
}

interface CommunityCardProps {
  readonly preset: ThemePreset;
  readonly author: string;
  readonly version: string;
  readonly isActive: boolean;
  readonly isPending: boolean;
  readonly onActivate: () => void;
}

function CommunityCard({
  preset,
  author,
  version,
  isActive,
  isPending,
  onActivate,
  t,
}: CommunityCardProps & { readonly t: ReturnType<typeof useTranslation>['t'] }) {
  return (
    <Card
      data-od-id={`theme-marketplace-card-${preset.id}`}
      className={cn(
        'overflow-hidden',
        isActive && 'border-foreground/60 ring-1 ring-foreground/40',
      )}
    >
      <CardHeader className="pb-3">
        <div className="flex items-start justify-between gap-3">
          <div className="min-w-0 space-y-1">
            <CardTitle className="text-sm">{preset.name}</CardTitle>
            <p className="text-xs text-muted-foreground">{preset.note}</p>
          </div>
          {isActive && <Badge variant="secondary">{t('admin.themeMarketplace.activeTheme')}</Badge>}
        </div>
      </CardHeader>
      <CardContent className="space-y-3">
        <div className="flex gap-1.5" aria-hidden>
          <span
            className="size-8 rounded-md border border-border"
            style={{ backgroundColor: preset.tokens.background }}
            title="background"
          />
          <span
            className="size-8 rounded-md border border-border"
            style={{ backgroundColor: preset.tokens.foreground }}
            title="foreground"
          />
          <span
            className="size-8 rounded-md border border-border"
            style={{ backgroundColor: preset.tokens.accent }}
            title="accent"
          />
          <span
            className="size-8 rounded-md border border-border"
            style={{ backgroundColor: preset.tokens['ok-soft'] }}
            title="ok"
          />
          <span
            className="size-8 rounded-md border border-border"
            style={{ backgroundColor: preset.tokens['err-soft'] }}
            title="err"
          />
        </div>

        <dl className="grid grid-cols-2 gap-2 text-[11px]">
          <div>
            <dt className="text-muted-foreground">{t('admin.themeMarketplace.author')}</dt>
            <dd className="font-mono">{author}</dd>
          </div>
          <div>
            <dt className="text-muted-foreground">{t('admin.themeMarketplace.version')}</dt>
            <dd className="font-mono tabular-nums">v{version}</dd>
          </div>
        </dl>

        <Button
          variant={isActive ? 'outline' : 'default'}
          size="sm"
          onClick={onActivate}
          disabled={isPending || isActive}
          className="w-full"
        >
          {isPending
            ? t('common.loading')
            : isActive
              ? t('admin.themeMarketplace.activeTheme')
              : t('admin.themeMarketplace.activate')}
        </Button>
      </CardContent>
    </Card>
  );
}

function AdminThemeMarketplacePage() {
  const { t } = useTranslation();
  const navigate = useNavigate();

  const presets = useMemo<readonly ThemePreset[]>(() => listPresets(), []);
  const communityQuery = useCommunityThemes();
  const activate = useActivateTheme();
  const deactivate = useDeactivateTheme();
  const activeThemeId = useActiveThemeId();

  // Local "active theme id" mirror of the backend so the highlight
  // updates immediately on click. Reset when the mutation settles.
  const [optimisticActiveId, setOptimisticActiveId] = useState<string | null>(
    activeThemeId,
  );

  useEffect(() => {
    setOptimisticActiveId(activeThemeId);
  }, [activeThemeId]);

  const handleActivate = (id: string) => {
    setOptimisticActiveId(id);
    activate.mutate(id, {
      onError: () => {
        setOptimisticActiveId(activeThemeId);
      },
      onSuccess: (response) => {
        const resolvedId = response.themeId;
        setOptimisticActiveId(resolvedId);
        const preset = getPreset(resolvedId);
        activateBuiltIn(preset);
      },
    });
  };

  const handleDeactivate = () => {
    setOptimisticActiveId(null);
    deactivate.mutate({
      onError: () => {
        setOptimisticActiveId(activeThemeId);
      },
    });
  };

  const community = communityQuery.data ?? [];
  const isLoading = communityQuery.isLoading;
  const hasError = communityQuery.isError;

  return (
    <PageTemplate
      title={t('admin.themeMarketplace.title')}
      description={t('admin.themeMarketplace.description')}
      width="default"
      data-od-id="admin-theme-marketplace"
      actions={
        <Button
          variant="outline"
          size="sm"
          onClick={() => {
            void navigate({ to: '/' });
          }}
        >
          {t('common.back')}
        </Button>
      }
    >
      <Card data-od-id="admin-theme-marketplace-community">
        <CardHeader>
          <CardTitle className="flex items-center gap-2">
            <Storefront className="size-4" />
            {t('admin.themeMarketplace.community')}
          </CardTitle>
        </CardHeader>
        <CardContent>
          {isLoading ? (
            <div className="text-sm text-muted-foreground">{t('common.loading')}</div>
          ) : hasError ? (
            <div className="text-sm text-err-ink">
              {t('admin.themeMarketplace.loadError')}
            </div>
          ) : community.length === 0 ? (
            <EmptyState
              title={t('admin.themeMarketplace.empty')}
              description={t('admin.themeMarketplace.description')}
              icon={Storefront}
            />
          ) : (
            <div className="grid grid-cols-1 gap-3 sm:grid-cols-2 lg:grid-cols-3">
              {community.map((theme) => (
                <CommunityCard
                  key={theme.id}
                  preset={theme}
                  author={theme.author}
                  version={theme.version}
                  isActive={optimisticActiveId === theme.id}
                  isPending={activate.isPending}
                  onActivate={() => {
                    handleActivate(theme.id);
                  }}
                  t={t}
                />
              ))}
            </div>
          )}
          {optimisticActiveId !== null && (
            <div className="mt-4 flex justify-end">
              <Button
                variant="ghost"
                size="sm"
                onClick={handleDeactivate}
                disabled={deactivate.isPending}
              >
                {t('admin.themeMarketplace.resetToDefaults')}
              </Button>
            </div>
          )}
        </CardContent>
      </Card>

      <Card className="mt-4" data-od-id="admin-theme-marketplace-built-in">
        <CardHeader>
          <CardTitle className="flex items-center gap-2">
            <Palette className="size-4" />
            {t('admin.themeMarketplace.builtIn')}
          </CardTitle>
        </CardHeader>
        <CardContent>
          <div className="grid grid-cols-1 gap-2 sm:grid-cols-3">
            {presets.map((preset) => {
              const active = optimisticActiveId === preset.id;
              return (
                <button
                  key={preset.id}
                  type="button"
                  onClick={() => {
                    handleActivate(preset.id);
                  }}
                  className={cn(
                    'flex flex-col items-start gap-1 rounded-lg border p-3 text-left text-xs',
                    active
                      ? 'border-foreground/60 ring-1 ring-foreground/40 bg-surface-2'
                      : 'border-border hover:border-foreground/30',
                  )}
                >
                  <span className="font-medium">{preset.name}</span>
                  <span className="text-muted-foreground">{preset.note}</span>
                </button>
              );
            })}
          </div>
        </CardContent>
      </Card>

      <p className="mt-4 text-[11px] text-muted-foreground">
        <Link to="/admin/branding" className="underline">
          {t('admin.branding.title')}
        </Link>{' '}
        — {t('admin.themeMarketplace.brandingHint')}
      </p>
    </PageTemplate>
  );
}
