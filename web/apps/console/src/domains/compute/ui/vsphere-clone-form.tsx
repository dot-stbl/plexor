import { useState } from 'react';
import { Link, useNavigate } from '@tanstack/react-router';
import { useTranslation } from 'react-i18next';
import { toast } from 'sonner';
import { Add } from '@nine-thirty-five/material-symbols-react/rounded/700';
import { Button } from '@/shared/ui/primitives/button';
import { Input } from '@/shared/ui/primitives/input';
import { FieldRow } from '@/shared/ui/primitives/field-row';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/shared/ui/primitives/card';
import { useVSphereClone } from '../api/use-vsphere-clone';

/**
 * Clone form for vSphere. Three required-or-optional text inputs
 * (template name, new VM name, folder path), one submit button.
 * On success: toasts + navigates back to the inventory list.
 *
 * v1 limitations surfaced in copy:
 *   - The backend resolves template name against the latest cached
 *     snapshot; a template never cloned before can't be found here.
 *   - Folder path is optional; vCenter picks the default folder
 *     when omitted.
 */
export function VSphereCloneForm() {
  const { t } = useTranslation();
  const navigate = useNavigate();
  const clone = useVSphereClone();
  const [templateName, setTemplateName] = useState('');
  const [name, setName] = useState('');
  const [folderPath, setFolderPath] = useState('');

  const canSubmit =
    templateName.trim().length > 0 && name.trim().length > 0 && !clone.isPending;

  const handleSubmit = (event: React.FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    if (!canSubmit) return;
    clone.mutate({
      templateName: templateName.trim(),
      name: name.trim(),
      folderPath: folderPath.trim() ? folderPath.trim() : null,
    });
  };

  if (clone.isSuccess) {
    toast.success(t('vsphere.clone.success', { name: name.trim() }), {
      description: t('vsphere.clone.morefField', {
        moref: clone.data?.vmMoref ?? '—',
      }),
    });
    void navigate({ to: '/vsphere' });
  } else if (clone.isError) {
    const detail =
      typeof clone.error === 'object' && clone.error !== null && 'data' in clone.error
        ? JSON.stringify((clone.error as { data?: unknown }).data)
        : clone.error instanceof Error
          ? clone.error.message
          : t('vsphere.clone.errorGeneric');
    toast.error(t('vsphere.clone.errorTitle'), { description: detail });
  }

  return (
    <form
      data-od-id="vsphere-clone-form"
      onSubmit={handleSubmit}
      className="flex flex-col gap-3"
    >
      <Card>
        <CardHeader className="border-b border-border">
          <CardTitle className="text-sm">{t('vsphere.clone.card.title')}</CardTitle>
          <CardDescription>{t('vsphere.clone.card.description')}</CardDescription>
        </CardHeader>
        <CardContent className="flex flex-col gap-2">
          <FieldRow
            label={t('vsphere.clone.templateName')}
            htmlFor="vsphere-template-name"
            required
            help={t('vsphere.clone.templateNameHelp')}
          >
            <Input
              id="vsphere-template-name"
              value={templateName}
              onChange={(event) => setTemplateName(event.target.value)}
              placeholder={t('vsphere.clone.templateNamePlaceholder')}
              autoComplete="off"
              required
            />
          </FieldRow>
          <FieldRow
            label={t('vsphere.clone.name')}
            htmlFor="vsphere-vm-name"
            required
            help={t('vsphere.clone.nameHelp')}
          >
            <Input
              id="vsphere-vm-name"
              value={name}
              onChange={(event) => setName(event.target.value)}
              placeholder={t('vsphere.clone.namePlaceholder')}
              autoComplete="off"
              required
            />
          </FieldRow>
          <FieldRow
            label={t('vsphere.clone.folderPath')}
            htmlFor="vsphere-folder-path"
            description={t('vsphere.clone.folderPathHelp')}
          >
            <Input
              id="vsphere-folder-path"
              value={folderPath}
              onChange={(event) => setFolderPath(event.target.value)}
              placeholder={t('vsphere.clone.folderPathPlaceholder')}
              autoComplete="off"
              className="font-mono"
            />
          </FieldRow>
        </CardContent>
      </Card>

      <div className="flex items-center justify-between">
        <Button
          type="button"
          variant="outline"
          nativeButton={false}
          render={<Link to="/vsphere" />}
        >
          {t('common.cancel')}
        </Button>
        <Button type="submit" disabled={!canSubmit}>
          <Add />
          {t('vsphere.clone.submit')}
        </Button>
      </div>
    </form>
  );
}
