import { useMemo } from 'react';
import { useTranslation } from 'react-i18next';
import { useListVms } from '@/shared/api';
import { Stat } from '@/shared/ui/primitives/stat';

/**
 * Dashboard widget — fleet aggregate stats.
 *
 * Wire: GET /api/v1/vms → listVmsHandler → 10-VM fleet. Computes
 * (total / running / total vCPU / total RAM) client-side so the
 * single API call feeds both this card and the donut above. No
 * new endpoint, no extra round-trip.
 */
export function FleetTotalsStats() {
  const { t } = useTranslation();
  const { data, isPending } = useListVms();

  const totals = useMemo(() => {
    const items = data?.items ?? [];
    const running = items.filter((vm) => vm.status === 'running').length;
    const vcpu = items.reduce((sum, vm) => sum + vm.vcpu, 0);
    const ramGb = items.reduce((sum, vm) => sum + vm.ramGb, 0);
    return { total: items.length, running, vcpu, ramGb };
  }, [data]);

  if (isPending) {
    return (
      <div className="grid grid-cols-2 gap-2 lg:grid-cols-4">
        <Stat label={t('dashboard.fleet.total')} value="—" />
        <Stat label={t('dashboard.fleet.running')} value="—" />
        <Stat label={t('dashboard.fleet.cpu')} value="—" />
        <Stat label={t('dashboard.fleet.ram')} value="—" />
      </div>
    );
  }

  return (
    <div className="grid grid-cols-2 gap-2 lg:grid-cols-4">
      <Stat label={t('dashboard.fleet.total')} value={totals.total} />
      <Stat label={t('dashboard.fleet.running')} value={totals.running} context={t('dashboard.fleet.ofTotal', { total: totals.total })} />
      <Stat label={t('dashboard.fleet.cpu')} value={`${totals.vcpu} vCPU`} />
      <Stat label={t('dashboard.fleet.ram')} value={`${totals.ramGb} GB`} />
    </div>
  );
}
