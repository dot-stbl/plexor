import type { Meta, StoryObj } from '@storybook/react-vite';
import { useMemo } from 'react';
import { useTranslation } from 'react-i18next';
import { Download } from '@nine-thirty-five/material-symbols-react/rounded/700';
import { Receipt } from '@nine-thirty-five/material-symbols-react/rounded/700';
import { PageTemplate } from '@/shared/ui/app-shell';
import { Button } from '@/shared/ui/primitives/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/shared/ui/primitives/card';
import { Stat } from '@/shared/ui/primitives/stat';
import { Progress } from '@/shared/ui/primitives/progress';
import { DataTable } from '@/shared/ui/data-table';
import { EmptyState } from '@/shared/ui/primitives/empty-state';
import { getInvoiceColumns } from '@/domains/billing';
import {
  getBillingSnapshot,
  type BillingSnapshot,
} from '@/shared/api/mocks/handmade/billing';

/**
 * /billing page stories.
 *
 * The route reads from a handmade mock via `useBilling()`; stories use the
 * same factory so the page body renders identically to the real page. Two
 * states:
 *   Default: full page with empty invoices card (the realistic first-run state).
 *   WithInvoices: same page but with a few example invoices, so the table
 *                 path is captured too.
 */

function BillingPageBody({ snapshot }: { snapshot: BillingSnapshot }) {
  const { t } = useTranslation();
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

const meta = {
  title: 'Pages/Billing',
  parameters: { layout: 'fullscreen' },
} satisfies Meta;

export default meta;
type Story = StoryObj<typeof meta>;

/** Default: realistic first-run state (no invoices yet). */
export const Default: Story = {
  render: () => <BillingPageBody snapshot={getBillingSnapshot()} />,
};

/** WithInvoices: a few historical invoices so the table path is captured. */
export const WithInvoices: Story = {
  render: () => {
    const base = getBillingSnapshot();
    const augmented: BillingSnapshot = {
      ...base,
      invoices: [
        {
          id: 'inv-2026-08',
          number: 'PLX-2026-008',
          issuedAt: '2026-09-01T00:00:00Z',
          amountMinor: 0,
          currency: 'EUR',
          status: 'paid',
        },
        {
          id: 'inv-2026-07',
          number: 'PLX-2026-007',
          issuedAt: '2026-08-01T00:00:00Z',
          amountMinor: 0,
          currency: 'EUR',
          status: 'paid',
        },
        {
          id: 'inv-2026-06',
          number: 'PLX-2026-006',
          issuedAt: '2026-07-01T00:00:00Z',
          amountMinor: 0,
          currency: 'EUR',
          status: 'void',
        },
      ],
    };
    return <BillingPageBody snapshot={augmented} />;
  },
};
