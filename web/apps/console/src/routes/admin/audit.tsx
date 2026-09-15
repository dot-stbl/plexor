import { createFileRoute, useNavigate } from '@tanstack/react-router';
import { useEffect, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { History } from '@nine-thirty-five/material-symbols-react/rounded/700';
import { PageTemplate } from '@/shared/ui/app-shell';
import { Button } from '@/shared/ui/primitives/button';
import { Input } from '@/shared/ui/primitives/input';
import { Label } from '@/shared/ui/primitives/label';
import { Card, CardContent, CardHeader, CardTitle } from '@/shared/ui/primitives/card';
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from '@/shared/ui/primitives/table';
import { EmptyState } from '@/shared/ui/primitives/empty-state';
import { routeHead } from '@/shared/lib/route-head';
import { useAudit } from '@/features/audit/use-audit';
import type { AuditEntry, AuditQueryParams } from '@/features/audit/audit-types';

/**
 * AdminAuditPage — tenant-scoped admin read surface for the
 * append-only audit log. The page exposes:
 *
 *   1. Filter row — action (dot.case wire name), actor (user id),
 *      and a `since` (lower-bound on occurredAt). Filters are
 *      client-side state; the user hits Apply to commit them to
 *      the query key so the cache invalidates + the page refetches.
 *   2. Results table — id, action, actor, target kind/id,
 *      occurredAt. Rows render newest-first (matches the backend
 *      ORDER BY occurred_at DESC).
 *   3. Load more — offset-style pagination. The "before" cursor
 *      comes from the oldest row currently visible; the user clicks
 *      Load more to fetch the next page and append it. Stops when
 *      the next page returns < limit rows.
 *
 * Reads the existing GET /api/v1/audit endpoint (Phase 5.2). No
 * write surface yet — audit retention (Phase 5.3) is a backend
 * BackgroundService, not an admin action.
 */

export const Route = createFileRoute('/admin/audit')({
  component: AdminAuditPage,
  ...routeHead('Audit'),
});

const PAGE_SIZE = 100;

interface FilterState {
  action: string;
  actorUserId: string;
  /** ISO timestamp (date-only is fine — server treats it as midnight UTC). */
  since: string;
}

const EMPTY_FILTERS: FilterState = {
  action: '',
  actorUserId: '',
  since: '',
};

function toParams(filters: FilterState, before: string | null): AuditQueryParams {
  return {
    action: filters.action.trim() ? filters.action.trim() : null,
    actorUserId: filters.actorUserId.trim() ? filters.actorUserId.trim() : null,
    since: filters.since.trim() ? `${filters.since.trim()}T00:00:00Z` : null,
    before,
    limit: PAGE_SIZE,
  };
}

function formatTimestamp(iso: string): string {
  // Render UTC. ISO format — the table is a fast scan; the admin
  // doesn't need localised formatting for an audit timeline.
  const parsed = new Date(iso);
  if (Number.isNaN(parsed.getTime())) return iso;
  return parsed.toISOString().replace('T', ' ').replace(/\.\d{3}Z$/, 'Z');
}

function AdminAuditPage() {
  const { t } = useTranslation();
  const navigate = useNavigate();

  const [filters, setFilters] = useState<FilterState>(EMPTY_FILTERS);
  const [appliedFilters, setAppliedFilters] = useState<FilterState>(EMPTY_FILTERS);
  const [beforeCursor, setBeforeCursor] = useState<string | null>(null);
  const [rows, setRows] = useState<AuditEntry[]>([]);

  const auditQuery = useAudit(toParams(appliedFilters, beforeCursor));

  // When the page-load result lands, replace the rows; when a
  // Load-more result lands, append. Distinguishing the two by
  // cursor state — first page has null cursor, paginated loads
  // have a non-null cursor.
  useEffect(() => {
    const incoming = auditQuery.data;
    if (!incoming) return;
    if (beforeCursor === null) {
      setRows(incoming);
      return;
    }
    setRows((prev) => {
      const seen = new Set(prev.map((r) => r.id));
      const appended = incoming.filter((r) => !seen.has(r.id));
      return appended.length === 0 ? prev : [...prev, ...appended];
    });
  }, [auditQuery.data, beforeCursor]);

  const handleApply = () => {
    setAppliedFilters(filters);
    setBeforeCursor(null);
    setRows([]);
  };

  const handleReset = () => {
    setFilters(EMPTY_FILTERS);
    setAppliedFilters(EMPTY_FILTERS);
    setBeforeCursor(null);
    setRows([]);
  };

  const handleLoadMore = () => {
    if (rows.length === 0) return;
    // The cursor is the occurredAt of the oldest row we have.
    // `before` is a strict less-than boundary on the backend, so
    // the next page picks up rows strictly older than that.
    const oldest = rows[rows.length - 1];
    setBeforeCursor(oldest.occurredAt);
  };

  const isLoading = auditQuery.isLoading;
  const isFetchingMore = beforeCursor !== null && auditQuery.isFetching;

  return (
    <PageTemplate
      title={t('admin.audit.title')}
      description={t('admin.audit.description')}
      width="full"
      data-od-id="admin-audit"
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
      <Card data-od-id="admin-audit-filters">
        <CardHeader>
          <CardTitle className="flex items-center gap-2">
            <History className="size-4" />
            {t('admin.audit.filters.title')}
          </CardTitle>
        </CardHeader>
        <CardContent className="space-y-3">
          <div className="grid grid-cols-1 gap-3 sm:grid-cols-3">
            <div className="space-y-1.5">
              <Label htmlFor="audit-filter-action" className="text-xs font-medium">
                {t('admin.audit.filters.actionLabel')}
              </Label>
              <Input
                id="audit-filter-action"
                value={filters.action}
                onChange={(event) => {
                  setFilters((prev) => ({ ...prev, action: event.target.value }));
                }}
                placeholder="quotas.assignment.changed"
                maxLength={256}
              />
            </div>
            <div className="space-y-1.5">
              <Label htmlFor="audit-filter-actor" className="text-xs font-medium">
                {t('admin.audit.filters.actorLabel')}
              </Label>
              <Input
                id="audit-filter-actor"
                value={filters.actorUserId}
                onChange={(event) => {
                  setFilters((prev) => ({ ...prev, actorUserId: event.target.value }));
                }}
                placeholder="00000000-0000-0000-0000-000000000000"
                maxLength={64}
              />
            </div>
            <div className="space-y-1.5">
              <Label htmlFor="audit-filter-since" className="text-xs font-medium">
                {t('admin.audit.filters.sinceLabel')}
              </Label>
              <Input
                id="audit-filter-since"
                type="date"
                value={filters.since}
                onChange={(event) => {
                  setFilters((prev) => ({ ...prev, since: event.target.value }));
                }}
              />
            </div>
          </div>
          <div className="flex justify-end gap-2">
            <Button variant="outline" size="sm" onClick={handleReset}>
              {t('common.cancel')}
            </Button>
            <Button variant="default" size="sm" onClick={handleApply}>
              {t('admin.audit.filters.apply')}
            </Button>
          </div>
        </CardContent>
      </Card>

      <Card className="mt-4" data-od-id="admin-audit-results">
        <CardContent className="p-0">
          {isLoading ? (
            <div className="p-6 text-sm text-muted-foreground">
              {t('common.loading')}
            </div>
          ) : auditQuery.isError ? (
            <div className="p-6 text-sm text-err-ink">
              {t('admin.audit.error.fetch')}
            </div>
          ) : rows.length === 0 ? (
            <div className="p-6">
              <EmptyState
                title={t('admin.audit.table.empty')}
                description={t('admin.audit.description')}
                icon={History}
              />
            </div>
          ) : (
            <>
              <Table>
                <TableHeader>
                  <TableRow>
                    <TableHead>{t('admin.audit.table.headers.action')}</TableHead>
                    <TableHead>{t('admin.audit.table.headers.actor')}</TableHead>
                    <TableHead>{t('admin.audit.table.headers.target')}</TableHead>
                    <TableHead>{t('admin.audit.table.headers.occurredAt')}</TableHead>
                  </TableRow>
                </TableHeader>
                <TableBody>
                  {rows.map((entry) => (
                    <TableRow key={entry.id}>
                      <TableCell className="font-mono text-xs">
                        {entry.action}
                      </TableCell>
                      <TableCell className="font-mono text-xs text-muted-foreground">
                        {entry.actorUserId ?? '—'}
                      </TableCell>
                      <TableCell className="font-mono text-xs text-muted-foreground">
                        {entry.targetKind}
                        {entry.targetId ? `:${entry.targetId}` : ''}
                      </TableCell>
                      <TableCell className="font-mono text-xs tabular-nums">
                        {formatTimestamp(entry.occurredAt)}
                      </TableCell>
                    </TableRow>
                  ))}
                </TableBody>
              </Table>
              <div className="flex justify-center border-t border-border p-3">
                <Button
                  variant="outline"
                  size="sm"
                  onClick={handleLoadMore}
                  disabled={isFetchingMore || auditQuery.data?.length !== PAGE_SIZE}
                >
                  {isFetchingMore ? t('common.loading') : t('admin.audit.loadMore')}
                </Button>
              </div>
            </>
          )}
        </CardContent>
      </Card>
    </PageTemplate>
  );
}
