// Handmade mock — billing page data for the self-hosted edition.
//
// Plexor self-hosted doesn't bill: there is no /api/v1/billing endpoint.
// The /billing page is a read-only projection of resource usage and a
// mirror of the contract shape we'll wire once billing automation lands
// (Phase 6+). The empty case (no rows / no invoices) is the realistic
// first-run state — see `BillingEmpty` for the on-screen copy.
//
// TODO(contract): when the billing endpoints land (Phase 6+), replace
// this with kubb-generated handlers and delete this module.

/** Self-hosted edition — fixed. */
export type BillingEdition = 'community' | 'standard' | 'enterprise';

export interface BillingPlan {
  edition: BillingEdition;
  /** ISO date the plan next renews. */
  renewsAt: string;
  /** Active seat count (humans with a session in the last 30 days). */
  seatsUsed: number;
  seatsLimit: number;
  support: 'community' | 'standard' | 'business';
}

export interface BillingUsageRow {
  /** Stable i18n key resolved by the page. */
  metric: 'compute' | 'storage' | 'egress';
  unit: string;
  used: number;
  /** Soft cap surfaced in the UI; the host doesn't enforce it. */
  limit: number;
}

export type InvoiceStatus = 'paid' | 'open' | 'void';

export interface BillingInvoice {
  id: string;
  /** Human-readable number ("PLX-2026-009"). */
  number: string;
  issuedAt: string;
  /** Minor units (cents) — integer to avoid float drift. */
  amountMinor: number;
  currency: 'EUR' | 'USD' | 'RUB';
  status: InvoiceStatus;
}

export interface BillingPaymentMethod {
  kind: 'bank-transfer';
  /** Last 4 of the IBAN — the page doesn't render the full IBAN. */
  ibanSuffix: string;
  reference: string;
}

export interface BillingSnapshot {
  plan: BillingPlan;
  usage: BillingUsageRow[];
  invoices: BillingInvoice[];
  payment: BillingPaymentMethod;
}

const SNAPSHOT: BillingSnapshot = {
  plan: {
    edition: 'community',
    renewsAt: '2026-09-30T00:00:00Z',
    seatsUsed: 4,
    seatsLimit: 10,
    support: 'community',
  },
  usage: [
    { metric: 'compute', unit: 'vCPU hours', used: 1840, limit: 5000 },
    { metric: 'storage', unit: 'GB-month', used: 312, limit: 1000 },
    { metric: 'egress', unit: 'GB', used: 47, limit: 250 },
  ],
  // The realistic first-run state is an empty invoice list. The page
  // distinguishes "no invoices yet" from "invoices failed to load" via
  // the explicit empty state — see BillingEmptyInvoices.
  invoices: [],
  payment: {
    kind: 'bank-transfer',
    ibanSuffix: '4300',
    reference: 'PLX-CUST-0001',
  },
};

export function getBillingSnapshot(): BillingSnapshot {
  return SNAPSHOT;
}

/**
 * Render an integer minor-units amount as a decimal currency string.
 * No Intl.NumberFormat because the test-runner headless Chromium is
 * pinned and we want identical pixels across machines.
 */
export function formatAmount(amountMinor: number, currency: BillingInvoice['currency']): string {
  const major = (amountMinor / 100).toFixed(2);
  return currency === 'RUB' ? `${major} ₽` : `${currency} ${major}`;
}
