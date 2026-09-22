import { useMemo } from 'react';
import { getBillingSnapshot, type BillingSnapshot, type BillingUsageRow } from '@/shared/api/mocks/handmade/billing';

/**
 * Hook поверх handmade mock для биллинга. Возвращает синхронный snapshot —
 * биллинг для self-hosted не сетевой, и пока нет kubb-эндпоинта, эта
 * поверхность чистая. Когда Phase 6+ добавит /api/v1/billing, модуль
 * переедет на kubb-генерированный query hook, а сигнатура хука останется
 * (snapshot — это контракт, не источник).
 */
export function useBilling(): { snapshot: BillingSnapshot; isPending: false; error: null } {
  return useMemo(() => ({ snapshot: getBillingSnapshot(), isPending: false, error: null }), []);
}

/** Convenience selector: usage row by metric key. */
export function getUsageRow(snapshot: BillingSnapshot, metric: BillingUsageRow['metric']): BillingUsageRow | undefined {
  return snapshot.usage.find((row) => row.metric === metric);
}
