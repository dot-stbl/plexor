import { createFileRoute } from '@tanstack/react-router';
import { Download } from '@nine-thirty-five/material-symbols-react/rounded/700';
import { useTranslation } from 'react-i18next';
import { useMemo } from 'react';
import { PageTemplate } from '@/shared/ui/app-shell';
import { Button } from '@/shared/ui/primitives/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/shared/ui/primitives/card';
import { Stat } from '@/shared/ui/primitives/stat';
import { Progress } from '@/shared/ui/primitives/progress';
import { DataTable } from '@/shared/ui/data-table';
import { EmptyState } from '@/shared/ui/primitives/empty-state';
import { Receipt } from '@nine-thirty-five/material-symbols-react/rounded/700';
import { getInvoiceColumns, useBilling } from '@/domains/billing';
import { routeHead } from '@/shared/lib/route-head';

/**
 * /billing — read-only projection of resource usage for the self-hosted
 * edition. Plexor self-hosted does not bill; this page mirrors the
 * contract shape we will eventually wire (Phase 6+), and lets a tenant
 * see what usage would be billed if they switched to a managed edition.
 *
 * Layout: 4 cards stacked — current plan, usage (3 progress bars),
 * invoices table (or empty state), payment method. All data comes from
 * a synchronous handmade mock until kubb handlers exist (see the
 * README in `shared/api/mocks/handmade/billing.ts`).
 */
export const Route = createFileRoute('/billing')({
  component: BillingPage,
  ...routeHead('Billing'),
});

function BillingPage() {
  const { t } = useTranslation();
  const { snapshot } = useBilling();
  const columns = useMemo(() => getInvoiceColumns(t), [t]);

  return (
    <PageTemplate
      title={t('billing.title')}
      description={t('billing.description')}
      width="wide"
      data-od-id="billing"
      actions={
        <Button variant="outline">
          <Download className="size-3.5" />
          {t('billing.export')}
        </Button>
      }
    >
      <div className="space-y-4">
        <Card data-od-id="billing-plan">
          <CardHeader>
            <CardTitle>{t('billing.currentPlan.heading')}</CardTitle>
          </CardHeader>
          <CardContent>
            <div className="grid grid-cols-1 gap-4 sm:grid-cols-2 lg:grid-cols-4">
              <Stat
                label={t('billing.currentPlan.heading')}
                value={t('billing.currentPlan.name', { edition: snapshot.plan.edition })}
                context={t('billing.currentPlan.renews')}
              />
              <Stat
                label={t('billing.currentPlan.seats')}
                value={t('billing.currentPlan.seatsValue', {
                  used: snapshot.plan.seatsUsed,
                  limit: snapshot.plan.seatsLimit,
                })}
              />
              <Stat
                label={t('billing.currentPlan.support')}
                value={t('billing.currentPlan.supportValue')}
              />
              <div className="flex items-center justify-end">
                <Button variant="outline" size="sm">
                  {t('billing.currentPlan.cta')}
                </Button>
              </div>
            </div>
          </CardContent>
        </Card>

        <Card data-od-id="billing-usage">
          <CardHeader>
            <CardTitle>{t('billing.usage.heading')}</CardTitle>
          </CardHeader>
          <CardContent>
            <div className="space-y-4">
              {snapshot.usage.map((row) => {
                const pct = Math.min(100, Math.round((row.used / row.limit) * 100));
                return (
                  <div key={row.metric} className="space-y-1.5">
                    <div className="flex items-baseline justify-between gap-2 text-xs">
                      <span className="font-medium text-foreground">
                        {t(`billing.usage.${row.metric}`)}
                      </span>
                      <span className="font-mono tabular-nums text-muted-foreground">
                        {row.used.toLocaleString('en-US')} / {row.limit.toLocaleString('en-US')} {row.unit}
                      </span>
                    </div>
                    <Progress value={pct} aria-label={t(`billing.usage.${row.metric}`)} />
                  </div>
                );
              })}
              <p className="text-xs text-muted-foreground">{t('billing.usage.resetNote')}</p>
            </div>
          </CardContent>
        </Card>

        <Card data-od-id="billing-invoices" className="p-0">
          <div className="px-(--card-spacing) pt-(--card-spacing)">
            <h3 className="font-heading text-sm font-medium">{t('billing.invoices.heading')}</h3>
          </div>
          <div className="px-(--card-spacing) pb-(--card-spacing)">
            {snapshot.invoices.length === 0 ? (
              <EmptyState
                compact
                icon={Receipt}
                title={t('billing.invoices.empty')}
                description={t('billing.invoices.emptyDescription')}
              />
            ) : (
              <DataTable columns={columns} data={snapshot.invoices} density="compact" />
            )}
          </div>
        </Card>

        <Card data-od-id="billing-payment">
          <CardHeader>
            <CardTitle>{t('billing.payment.heading')}</CardTitle>
          </CardHeader>
          <CardContent>
            <div className="grid grid-cols-1 gap-4 sm:grid-cols-3">
              <Stat
                label={t('billing.payment.method')}
                value={t('billing.payment.methodValue')}
              />
              <Stat
                label={t('billing.payment.details')}
                value={`•••• ${snapshot.payment.ibanSuffix}`}
                context={snapshot.payment.reference}
              />
              <div className="flex items-center justify-end">
                <Button variant="outline" size="sm">
                  {t('billing.payment.cta')}
                </Button>
              </div>
            </div>
          </CardContent>
        </Card>
      </div>
    </PageTemplate>
  );
}
