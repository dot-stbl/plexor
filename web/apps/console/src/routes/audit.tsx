import { createFileRoute } from '@tanstack/react-router';
import { useTranslation } from 'react-i18next';
import { useMemo } from 'react';
import { PageTemplate } from '@/shared/ui/app-shell';
import { DataTable } from '@/shared/ui/data-table';
import { AuditEmpty, getAuditColumns, useAudit } from '@/domains/audit';
import { routeHead } from '@/shared/lib/route-head';

/**
 * /audit — tenant-facing read surface for the audit timeline.
 *
 * Same kubb hook as the admin page (`/admin/audit`) but stripped down to
 * what a tenant user actually needs: a single chronological table, no
 * filters (a tenant has at most one orgId, the query is server-side
 * scoped). The kubb client enforces `orgId = caller.tenant_id`, so a
 * caller in org X never sees org Y — the host documents that in the
 * wire-comment on `getAudit`.
 */
export const Route = createFileRoute('/audit')({
  component: AuditPage,
  ...routeHead('Audit'),
});

function AuditPage() {
  const { t } = useTranslation();
  const { data, isPending, error } = useAudit({ limit: 100 });
  const rows = data ?? [];
  const columns = useMemo(() => getAuditColumns(t), [t]);

  return (
    <PageTemplate
      title={t('audit.title')}
      description={t('audit.description')}
      width="wide"
      data-od-id="audit"
    >
      {isPending ? (
        <div className="text-sm text-muted-foreground">{t('common.loading')}</div>
      ) : error ? (
        <div className="text-sm text-err-ink">{t('audit.error.fetch')}</div>
      ) : rows.length === 0 ? (
        <AuditEmpty />
      ) : (
        <DataTable columns={columns} data={rows} density="compact" />
      )}
    </PageTemplate>
  );
}
