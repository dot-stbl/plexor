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
export * from '@/mocks/billing/billing';
