// Handmade mock — page data for an endpoint NOT yet in the OpenAPI contract.
//
// TODO(contract): add GET /api/v1/compute/clusters (list + detail with
// nodes + join tokens) to contracts/plexor.openapi.yaml, then regenerate
// (web/tooling/codegen), wire the kubb handler in
// shared/api/mocks/handlers.ts, and delete this module.
export * from '@/mocks/fleet/clusters';
