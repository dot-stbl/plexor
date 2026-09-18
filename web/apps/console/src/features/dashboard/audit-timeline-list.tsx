import { useMemo } from 'react';
import { useTranslation } from 'react-i18next';
import { Link } from '@tanstack/react-router';
import { Card, CardContent, CardHeader, CardTitle } from '@/shared/ui/primitives/card';
import { useGetAudit } from '@/shared/api';

/**
 * Dashboard widget — recent audit events (last 8).
 *
 * Wire: GET /api/v1/audit → getAuditHandler → hand-curated timeline
 * of 10 dot.case action verbs (vm.lifecycle.*, quotas.*, node.*,
 * branding.*, auth.*). Renders in `occurredAt` descending order so
 * the most recent event is at the top. Each row links to the audit
 * page (`/audit`) where the full filterable timeline lives.
 */
export function AuditTimelineList() {
  const { t } = useTranslation();
  const { data, isPending } = useGetAudit();

  const recent = useMemo(() => (data ?? []).slice(0, 8), [data]);

  return (
    <Card data-od-id="dashboard-audit-timeline" className="gap-0 p-0">
      <CardHeader className="gap-0.5 border-b border-border p-4">
        <div className="flex items-center justify-between gap-2">
          <CardTitle className="text-sm">{t('dashboard.audit.title')}</CardTitle>
          <Link to="/audit" className="text-xs text-muted-foreground hover:text-foreground">
            {t('dashboard.audit.viewAll')}
          </Link>
        </div>
      </CardHeader>
      <CardContent className="p-0">
        {isPending || recent.length === 0 ? (
          <div className="flex h-32 items-center justify-center text-xs text-muted-foreground">
            {t('common.loading')}
          </div>
        ) : (
          <ul className="divide-y divide-border">
            {recent.map((entry) => (
              <li key={entry.id} className="flex items-start gap-3 px-4 py-2.5">
                <span
                  aria-hidden
                  className="mt-1 size-1.5 shrink-0 rounded-full bg-foreground/40"
                />
                <div className="min-w-0 flex-1">
                  <div className="flex items-baseline gap-2">
                    <span className="truncate font-mono text-xs text-foreground">
                      {entry.action}
                    </span>
                    <span className="shrink-0 font-mono text-[11px] text-muted-foreground tabular-nums">
                      {formatRelative(entry.occurredAt, t('dashboard.audit.justNow'))}
                    </span>
                  </div>
                  {entry.targetId && (
                    <div className="truncate font-mono text-[11px] text-muted-foreground">
                      {entry.targetKind} · {entry.targetId}
                    </div>
                  )}
                </div>
              </li>
            ))}
          </ul>
        )}
      </CardContent>
    </Card>
  );
}

function formatRelative(iso: string, justNowLabel: string): string {
  const then = new Date(iso).getTime();
  if (!Number.isFinite(then)) return '';
  const diffSec = Math.max(0, Math.floor((Date.now() - then) / 1000));
  if (diffSec < 60) return justNowLabel;
  if (diffSec < 3600) return `${Math.floor(diffSec / 60)}m`;
  if (diffSec < 86_400) return `${Math.floor(diffSec / 3600)}h`;
  return `${Math.floor(diffSec / 86_400)}d`;
}
