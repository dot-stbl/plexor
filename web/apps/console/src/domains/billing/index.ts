/**
 * Public surface of the billing domain. Routes import from
 * '@/domains/billing'; internal api/ui files stay unexported outside
 * this barrel (see .agents/docs/architecture/frontend-ddd.md).
 */
export { useBilling, getUsageRow } from './api/use-billing';
export type { BillingSnapshot } from '@/shared/api/mocks/handmade/billing';
export { getInvoiceColumns } from './ui/billing-columns';