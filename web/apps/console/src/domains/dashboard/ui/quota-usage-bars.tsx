import { useMemo } from 'react';
import { useTranslation } from 'react-i18next';
import { Bar, BarChart, XAxis, YAxis } from 'recharts';
import { Card, CardContent, CardHeader, CardTitle } from '@/shared/ui/primitives/card';
import { ChartContainer, ChartTooltip, ChartTooltipContent } from '@/shared/ui/primitives/chart';
import { useListQuotaUsage } from '@/shared/api';

/**
 * Dashboard widget — live quota usage vs limit.
 *
 * Wire: GET /api/v1/quotas/usage → listQuotaUsageHandler → hand-curated
 * 6 metrics (vm.count, vm.cpu_cores, vm.memory_gb, vm.disk_gb,
 * network.fip, storage.buckets). Each bar shows `used` against `limit`;
 * the bar fills toward `limit` so a near-ceiling metric is visually loud.
 */
export function QuotaUsageBars() {
  const { t } = useTranslation();
  const { data, isPending } = useListQuotaUsage();

  const rows = useMemo(
    () =>
      (data ?? [])
        .map((entry) => ({
          metric: entry.metric,
          used: entry.used,
          limit: entry.limit,
          ratio: entry.limit > 0 ? entry.used / entry.limit : 0,
        }))
        .sort((a, b) => b.ratio - a.ratio),
    [data],
  );

  return (
    <Card data-od-id="dashboard-quota-usage">
      <CardHeader className="border-b border-border">
        <CardTitle className="text-sm">{t('dashboard.quotaUsage.title')}</CardTitle>
      </CardHeader>
      <CardContent>
        {isPending || rows.length === 0 ? (
          <div className="flex h-32 items-center justify-center text-xs text-muted-foreground">
            {t('common.loading')}
          </div>
        ) : (
          <ChartContainer
            config={{ used: { label: t('dashboard.quotaUsage.used') } }}
            className="h-40"
          >
            <BarChart data={rows} layout="vertical" margin={{ left: 8, right: 8 }}>
              <YAxis
                type="category"
                dataKey="metric"
                width={110}
                tickLine={false}
                axisLine={false}
                fontSize={11}
              />
              <XAxis type="number" hide domain={[0, 'dataMax']} />
              <ChartTooltip
                content={
                  <ChartTooltipContent
                    formatter={(value) => `${value} / ${rows.find((r) => r.used === value)?.limit ?? '?'}`}
                  />
                }
              />
              <Bar dataKey="used" fill="var(--primary)" radius={[0, 4, 4, 0]} />
            </BarChart>
          </ChartContainer>
        )}
        <ul className="mt-3 grid grid-cols-1 gap-1 text-xs sm:grid-cols-2">
          {rows.map((row) => (
            <li key={row.metric} className="flex items-center justify-between gap-2">
              <span className="truncate text-muted-foreground">{row.metric}</span>
              <span className="font-mono tabular-nums">
                <span className="text-foreground">{row.used}</span>
                <span className="text-muted-foreground"> / {row.limit}</span>
              </span>
            </li>
          ))}
        </ul>
      </CardContent>
    </Card>
  );
}
