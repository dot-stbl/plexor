import { createFileRoute, useNavigate } from '@tanstack/react-router';
import { useEffect, useMemo, useState, type ReactNode } from 'react';
import { useTranslation } from 'react-i18next';
import { toast } from 'sonner';
import { Settings } from '@nine-thirty-five/material-symbols-react/rounded/700';
import { PageTemplate } from '@/shared/ui/app-shell';
import { Button } from '@/shared/ui/primitives/button';
import { Input } from '@/shared/ui/primitives/input';
import { Label } from '@/shared/ui/primitives/label';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/shared/ui/primitives/card';
import { cn } from '@/shared/lib/utils';
import { routeHead } from '@/shared/lib/route-head';
import { listPresets } from '@/shared/lib/themes/registry';
import { applyPreset } from '@/shared/lib/themes/apply-tokens';
import {
  useGlobalBranding,
  useOrgBranding,
  useUpdateGlobalBranding,
  useUpdateOrgBranding,
  useDeleteOrgBranding,
} from '@/features/branding/use-branding';
import type { GlobalBrandingConfig } from '@/features/branding/branding-types';

/**
 * AdminBrandingPage — operator-only console surface for the
 * Plexor.Modules.Branding backend (commit 2). The page exposes three
 * logical areas:
 *
 *   1. Operator global — saved via PUT /api/v1/branding/global
 *      (branding.update permission). Visible after the next reload.
 *   2. Per-org override — saved via PUT /api/v1/branding/org/{orgId}
 *      (branding.update). Only one org shown in v1; SaaS multi-org
 *      support lands when the org picker ships.
 *   3. Live preview — renders the current state of the two forms
 *      above with the brand name + accent. As-you-type updates
 *      give the admin instant feedback before they hit Save.
 *
 * Saving a mutation invalidates the relevant queries and reloads
 * the page so the runtime tokens (--background, --accent, etc.)
 * pick up the new values on the next paint.
 */

export const Route = createFileRoute('/admin/branding')({
  component: AdminBrandingPage,
  ...routeHead('Branding'),
});

const DEFAULT_ORG_ID = '00000000-0000-0000-0000-000000000001';

interface FormState {
  brandName: string;
  brandLogoUrl: string;
  brandFaviconUrl: string;
  defaultPresetId: string;
  customAccent: string;
}

function toForm(config: Pick<GlobalBrandingConfig, 'brandName' | 'brandLogoUrl' | 'brandFaviconUrl' | 'defaultPresetId' | 'customAccent'>): FormState {
  return {
    brandName: config.brandName,
    brandLogoUrl: config.brandLogoUrl ?? '',
    brandFaviconUrl: config.brandFaviconUrl ?? '',
    defaultPresetId: config.defaultPresetId,
    customAccent: config.customAccent ?? '',
  };
}

function AdminBrandingPage() {
  const { t } = useTranslation();
  const navigate = useNavigate();

  const presets = useMemo(() => listPresets(), []);

  const globalQuery = useGlobalBranding();
  const updateGlobal = useUpdateGlobalBranding();

  const [globalForm, setGlobalForm] = useState<FormState>({
    brandName: 'Plexor',
    brandLogoUrl: '',
    brandFaviconUrl: '',
    defaultPresetId: 'plexor-default-light',
    customAccent: '',
  });

  useEffect(() => {
    if (globalQuery.data) {
      setGlobalForm(toForm(globalQuery.data));
    }
  }, [globalQuery.data]);

  const orgQuery = useOrgBranding(DEFAULT_ORG_ID);
  const updateOrg = useUpdateOrgBranding(DEFAULT_ORG_ID);
  const deleteOrg = useDeleteOrgBranding(DEFAULT_ORG_ID);

  const [overrideEnabled, setOverrideEnabled] = useState(false);
  const [orgForm, setOrgForm] = useState<FormState>({
    brandName: '',
    brandLogoUrl: '',
    brandFaviconUrl: '',
    defaultPresetId: '',
    customAccent: '',
  });

  useEffect(() => {
    if (orgQuery.data) {
      setOverrideEnabled(true);
      setOrgForm({
        brandName: orgQuery.data.brandName ?? '',
        brandLogoUrl: orgQuery.data.brandLogoUrl ?? '',
        brandFaviconUrl: orgQuery.data.brandFaviconUrl ?? '',
        defaultPresetId: orgQuery.data.presetId ?? '',
        customAccent: orgQuery.data.customAccent ?? '',
      });
    }
  }, [orgQuery.data]);

  // Live preview — applies the form's preset + accent right now.
  const liveAccent = globalForm.customAccent || 'oklch(50% 0.02 250)';
  useEffect(() => {
    const preset = presets.find((p) => p.id === globalForm.defaultPresetId);
    if (!preset) return;
    applyPreset(preset);
    if (globalForm.customAccent) {
      document.documentElement.style.setProperty('--accent', globalForm.customAccent);
    }
  }, [globalForm.defaultPresetId, globalForm.customAccent, presets]);

  const handleSaveGlobal = async () => {
    try {
      await updateGlobal.mutateAsync({
        brandName: globalForm.brandName.trim(),
        brandLogoUrl: globalForm.brandLogoUrl.trim() || null,
        brandFaviconUrl: globalForm.brandFaviconUrl.trim() || null,
        defaultPresetId: globalForm.defaultPresetId,
        customAccent: globalForm.customAccent.trim() || null,
      });
      toast.success(t('admin.branding.savedToast'));
      window.setTimeout(() => {
        window.location.reload();
      }, 800);
    } catch (error) {
      const detail = error instanceof Error ? error.message : String(error);
      toast.error(t('admin.branding.savedError') + ': ' + detail);
    }
  };

  const handleSaveOrg = async () => {
    if (!overrideEnabled) return;
    try {
      await updateOrg.mutateAsync({
        brandName: orgForm.brandName.trim() || null,
        brandLogoUrl: orgForm.brandLogoUrl.trim() || null,
        brandFaviconUrl: orgForm.brandFaviconUrl.trim() || null,
        presetId: orgForm.defaultPresetId.trim() || null,
        customAccent: orgForm.customAccent.trim() || null,
      });
      toast.success(t('admin.branding.savedToast'));
      window.setTimeout(() => {
        window.location.reload();
      }, 800);
    } catch (error) {
      const detail = error instanceof Error ? error.message : String(error);
      toast.error(t('admin.branding.savedError') + ': ' + detail);
    }
  };

  const handleClearOrg = async () => {
    try {
      await deleteOrg.mutateAsync();
      setOverrideEnabled(false);
      setOrgForm({
        brandName: '',
        brandLogoUrl: '',
        brandFaviconUrl: '',
        defaultPresetId: '',
        customAccent: '',
      });
      toast.success(t('admin.branding.savedToast'));
      window.setTimeout(() => {
        window.location.reload();
      }, 800);
    } catch (error) {
      const detail = error instanceof Error ? error.message : String(error);
      toast.error(t('admin.branding.savedError') + ': ' + detail);
    }
  };

  return (
    <PageTemplate
      title={t('admin.branding.title')}
      description={t('admin.branding.description')}
      width="6xl"
      data-od-id="admin-branding"
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
      <div className="grid grid-cols-1 gap-4 lg:grid-cols-2">
        <Card data-od-id="admin-branding-global">
          <CardHeader>
            <CardTitle className="flex items-center gap-2">
              <Settings className="size-4" />
              {t('admin.branding.globalTitle')}
            </CardTitle>
            <CardDescription>{t('admin.branding.globalDescription')}</CardDescription>
          </CardHeader>
          <CardContent className="space-y-4">
            <Field id="global-brand-name" label={t('admin.branding.brandNameLabel')} help={t('admin.branding.brandNameHelp')}>
              <Input
                value={globalForm.brandName}
                onChange={(event) => {
                  setGlobalForm((prev) => ({
                    ...prev,
                    brandName: event.target.value,
                  }));
                }}
                maxLength={128}
                placeholder={t('admin.branding.livePreviewBrandName')}
              />
            </Field>

            <Field id="global-preset" label={t('admin.branding.presetLabel')}>
              <div className="grid grid-cols-1 gap-2 sm:grid-cols-3">
                {presets.map((preset) => {
                  const active = globalForm.defaultPresetId === preset.id;
                  return (
                    <button
                      key={preset.id}
                      type="button"
                      onClick={() => {
                        setGlobalForm((prev) => ({
                          ...prev,
                          defaultPresetId: preset.id,
                        }));
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
            </Field>

            <Field id="global-accent" label={t('admin.branding.accentLabel')} help={t('admin.branding.accentHelp')}>
              <Input
                value={globalForm.customAccent}
                onChange={(event) => {
                  setGlobalForm((prev) => ({
                    ...prev,
                    customAccent: event.target.value,
                  }));
                }}
                maxLength={64}
                placeholder={t('admin.branding.accentPlaceholder')}
              />
            </Field>

            <Field id="global-logo" label={t('admin.branding.logoLabel')} help={t('admin.branding.logoHelp')}>
              <Input
                value={globalForm.brandLogoUrl}
                onChange={(event) => {
                  setGlobalForm((prev) => ({
                    ...prev,
                    brandLogoUrl: event.target.value,
                  }));
                }}
                maxLength={2048}
                placeholder="https://acme.example/logo.svg"
              />
            </Field>

            <Field id="global-favicon" label={t('admin.branding.faviconLabel')} help={t('admin.branding.faviconHelp')}>
              <Input
                value={globalForm.brandFaviconUrl}
                onChange={(event) => {
                  setGlobalForm((prev) => ({
                    ...prev,
                    brandFaviconUrl: event.target.value,
                  }));
                }}
                maxLength={2048}
                placeholder="https://acme.example/favicon.svg"
              />
            </Field>

            <div className="flex justify-end gap-2">
              <Button
                variant="outline"
                size="sm"
                onClick={() => {
                  setGlobalForm(toForm({
                    brandName: 'Plexor',
                    brandLogoUrl: null,
                    brandFaviconUrl: null,
                    defaultPresetId: 'plexor-default-light',
                    customAccent: null,
                  }));
                }}
                disabled={updateGlobal.isPending}
              >
                {t('admin.branding.resetToDefaults')}
              </Button>
              <Button
                variant="default"
                size="sm"
                onClick={() => {
                  void handleSaveGlobal();
                }}
                disabled={updateGlobal.isPending}
              >
                {updateGlobal.isPending ? t('admin.branding.saving') : t('admin.branding.save')}
              </Button>
            </div>
          </CardContent>
        </Card>

        <Card data-od-id="admin-branding-org">
          <CardHeader>
            <CardTitle>{t('admin.branding.perOrgTitle')}</CardTitle>
            <CardDescription>{t('admin.branding.perOrgDescription')}</CardDescription>
          </CardHeader>
          <CardContent className="space-y-4">
            <div className="flex items-center justify-between rounded-lg border border-border p-3">
              <div className="min-w-0">
                <div className="text-xs font-medium">{t('admin.branding.currentOrgLabel')}</div>
                <div className="font-mono text-[11px] text-muted-foreground">
                  {DEFAULT_ORG_ID}
                </div>
              </div>
              <label className="flex items-center gap-2 text-xs">
                <input
                  type="checkbox"
                  checked={overrideEnabled}
                  onChange={(event) => {
                    setOverrideEnabled(event.target.checked);
                  }}
                />
                {t('admin.branding.overrideEnable')}
              </label>
            </div>

            <div className={cn('space-y-4', !overrideEnabled && 'pointer-events-none opacity-50')}>
              <Field id="org-brand-name" label={t('admin.branding.brandNameLabel')}>
                <Input
                  value={orgForm.brandName}
                  onChange={(event) => {
                    setOrgForm((prev) => ({
                      ...prev,
                      brandName: event.target.value,
                    }));
                  }}
                  maxLength={128}
                />
              </Field>

              <Field id="org-preset" label={t('admin.branding.presetLabel')}>
                <select
                  value={orgForm.defaultPresetId}
                  onChange={(event) => {
                    setOrgForm((prev) => ({
                      ...prev,
                      defaultPresetId: event.target.value,
                    }));
                  }}
                  className="h-9 w-full rounded-md border border-input bg-background px-2 text-sm"
                >
                  <option value="">— inherit —</option>
                  {presets.map((preset) => (
                    <option key={preset.id} value={preset.id}>
                      {preset.name}
                    </option>
                  ))}
                </select>
              </Field>

              <Field id="org-accent" label={t('admin.branding.accentLabel')}>
                <Input
                  value={orgForm.customAccent}
                  onChange={(event) => {
                    setOrgForm((prev) => ({
                      ...prev,
                      customAccent: event.target.value,
                    }));
                  }}
                  maxLength={64}
                  placeholder={t('admin.branding.accentPlaceholder')}
                />
              </Field>

              <Field id="org-logo" label={t('admin.branding.logoLabel')}>
                <Input
                  value={orgForm.brandLogoUrl}
                  onChange={(event) => {
                    setOrgForm((prev) => ({
                      ...prev,
                      brandLogoUrl: event.target.value,
                    }));
                  }}
                  maxLength={2048}
                />
              </Field>

              <Field id="org-favicon" label={t('admin.branding.faviconLabel')}>
                <Input
                  value={orgForm.brandFaviconUrl}
                  onChange={(event) => {
                    setOrgForm((prev) => ({
                      ...prev,
                      brandFaviconUrl: event.target.value,
                    }));
                  }}
                  maxLength={2048}
                />
              </Field>
            </div>

            <div className="flex justify-end gap-2">
              {overrideEnabled && (
                <Button
                  variant="outline"
                  size="sm"
                  onClick={() => {
                    void handleClearOrg();
                  }}
                  disabled={deleteOrg.isPending || updateOrg.isPending}
                >
                  {t('admin.branding.clearOverride')}
                </Button>
              )}
              <Button
                variant="default"
                size="sm"
                onClick={() => {
                  void handleSaveOrg();
                }}
                disabled={!overrideEnabled || updateOrg.isPending}
              >
                {updateOrg.isPending ? t('admin.branding.saving') : t('common.save')}
              </Button>
            </div>
          </CardContent>
        </Card>
      </div>

      <Card className="mt-4" data-od-id="admin-branding-preview">
        <CardHeader>
          <CardTitle>{t('admin.branding.livePreview')}</CardTitle>
          <CardDescription>{t('admin.branding.livePreviewHelp')}</CardDescription>
        </CardHeader>
        <CardContent>
          <div
            className="rounded-lg border border-border p-6"
            style={{
              backgroundColor: 'var(--background)',
              color: 'var(--foreground)',
              borderColor: 'var(--border)',
            }}
          >
            <div className="flex items-start justify-between gap-4">
              <div className="space-y-1">
                <div className="text-xs uppercase tracking-[0.06em] text-muted-foreground">
                  {t('admin.branding.livePreviewBrandName')}
                </div>
                <div className="text-2xl font-semibold tracking-tight">
                  {globalForm.brandName || t('admin.branding.livePreviewBrandName')}
                </div>
              </div>
              <div className="flex items-center gap-2">
                <Label className="text-xs text-muted-foreground">
                  {t('admin.branding.livePreviewAccent')}
                </Label>
                <span
                  className="inline-block size-8 rounded-md border border-border"
                  style={{ backgroundColor: liveAccent }}
                  aria-label={liveAccent}
                />
              </div>
            </div>
            <div className="mt-4 flex gap-2">
              <button
                type="button"
                className="rounded-md px-3 py-1.5 text-xs font-medium"
                style={{
                  backgroundColor: 'var(--accent)',
                  color: 'var(--accent-foreground)',
                }}
              >
                {t('admin.branding.save')}
              </button>
              <button
                type="button"
                className="rounded-md border border-border bg-background px-3 py-1.5 text-xs"
              >
                {t('common.cancel')}
              </button>
            </div>
          </div>
        </CardContent>
      </Card>

      {/* -------- Custom CSS escape hatch card --------------------- */}
      <Card className="mt-4" data-od-id="admin-branding-custom-css">
        <CardHeader>
          <CardTitle>{t('admin.branding.customCssTitle')}</CardTitle>
          <CardDescription>{t('admin.branding.customCssDescription')}</CardDescription>
        </CardHeader>
        <CardContent className="space-y-3">
          <Field
            id="custom-css-path"
            label={t('admin.branding.customCssFilePath')}
            help={t('admin.branding.customCssNote')}
          >
            <div className="flex items-center gap-2">
              <code className="flex-1 truncate rounded-md border border-border bg-muted/30 px-3 py-2 font-mono text-[11px]">
                /etc/plexor/custom.css
              </code>
              <Button
                variant="outline"
                size="sm"
                onClick={() => {
                  window.open('/custom.css', '_blank', 'noopener,noreferrer');
                }}
              >
                {t('admin.branding.customCssOpen')}
              </Button>
            </div>
          </Field>
          <p className="text-[11px] text-muted-foreground">
            {t('admin.branding.customCssWarning')}
          </p>
        </CardContent>
      </Card>
    </PageTemplate>
  );
}

/** Local Field helper — wraps Label + Input + optional help text
 * without depending on the missing Field/FieldRow primitives from
 * `@/shared/ui/primitives/field`. */
interface FieldProps {
  readonly id: string;
  readonly label: string;
  readonly help?: string;
  readonly children: ReactNode;
}

function Field({ id, label, help, children }: FieldProps) {
  return (
    <div className="space-y-1.5">
      <Label htmlFor={id} className="text-xs font-medium">
        {label}
      </Label>
      {children}
      {help && <p className="text-[11px] text-muted-foreground">{help}</p>}
    </div>
  );
}