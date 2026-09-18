import { useMemo } from 'react';
import { useTranslation } from 'react-i18next';
import { Cell, Pie, PieChart } from 'recharts';
import { Card, CardContent, CardHeader, CardTitle } from '@/shared/ui/primitives/card';
import { ChartContainer, ChartTooltip, ChartTooltipContent } from '@/shared/ui/primitives/chart';
import { useListVms } from '@/shared/api';
import type { VmStatus } from '@/shared/api';

/**
 * Dashboard widget — VM fleet grouped by lifecycle status.
 *
 * Wire: GET /api/v1/vms → listVmsHandler → hand-curated 10-VM fleet
 * (statuses: 7× running, 1× provisioning, 1× stopped, 1× error).
 * Renders a donut chart with the same DS color tokens the StatusPill
 * uses (--ok/--err/--warn/--idle) so the chart and the table read
 * identically under the active theme.
 */
export function VmStatusDonut() {
  const { t } = useTranslation();
  const { data, isPending } = useListVms();

  const grouped = useMemo(() => {
    const counts: Record<VmStatus, number> = {
      running: 0,
      stopped: 0,
      error: 0,
      provisioning: 0,
      idle: 0,
    };
    for (const vm of data?.items ?? []) {
      counts[vm.status] = (counts[vm.status] ?? 0) + 1;
    }
    return (Object.entries(counts) as [VmStatus, number][])
      .filter(([, n]) => n > 0)
      .map(([status, count]) => ({ status, count }));
  }, [data]);

  const chartConfig = useMemo(
    () =>
      Object.fromEntries(
        grouped.map((row) => [
          row.status,
          { label: t(`vms.status.${row.status}`, row.status), color: STATUS_FILL[row.status] },
        ]),
      ),
    [grouped, t],
  );

  return (
    <Card data-od-id="dashboard-vm-status" className="gap-0 p-0">
      <CardHeader className="gap-0.5 border-b border-border p-4">
        <CardTitle className="text-sm">{t('dashboard.vmStatus.title')}</CardTitle>
      </CardHeader>
      <CardContent className="p-4">
        {isPending || grouped.length === 0 ? (
          <div className="flex h-32 items-center justify-center text-xs text-muted-foreground">
            {t('common.loading')}
          </div>
        ) : (
          <ChartContainer config={chartConfig} className="mx-auto h-32">
            <PieChart>
              <ChartTooltip content={<ChartTooltipContent hideLabel />} />
              <Pie
                data={grouped}
                dataKey="count"
                nameKey="status"
                innerRadius={32}
                outerRadius={56}
                strokeWidth={2}
              >
                {grouped.map((row) => (
                  <Cell key={row.status} fill={STATUS_FILL[row.status]} />
                ))}
              </Pie>
            </PieChart>
          </ChartContainer>
        )}
        <ul className="mt-3 flex flex-wrap items-center gap-x-4 gap-y-1 text-xs">
          {grouped.map((row) => (
            <li key={row.status} className="flex items-center gap-1.5">
              <span
                aria-hidden
                className="size-2.5 rounded-[2px]"
                style={{ backgroundColor: STATUS_FILL[row.status] }}
              />
              <span className="text-muted-foreground">{t(`vms.status.${row.status}`, row.status)}</span>
              <span className="font-mono tabular-nums text-foreground">{row.count}</span>
            </li>
          ))}
        </ul>
      </CardContent>
    </Card>
  );
}

// Recharts `fill` accepts CSS values. Route through the DS status
// tokens (--ok/--err/--warn/--idle) so the donut recolors with the
// active theme and matches the StatusPill on /vms exactly. Adding
// a new VmStatus forces a compile error here until it is mapped.
const STATUS_FILL: Record<VmStatus, string> = {
  running: 'var(--ok)',
  stopped: 'var(--idle)',
  error: 'var(--err)',
  provisioning: 'var(--warn)',
  idle: 'var(--idle)',
};
