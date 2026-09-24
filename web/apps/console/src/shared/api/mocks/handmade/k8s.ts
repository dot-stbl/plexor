// Handmade mock — page data for an endpoint NOT yet in the OpenAPI contract.
//
// TODO(contract): add GET /api/v1/k8s/clusters (managed K3s cluster list)
// to contracts/plexor.openapi.yaml, then regenerate
// (web/tooling/codegen), wire the kubb handler in
// shared/api/mocks/handlers.ts, and delete this module.
//
// Moved verbatim from features/k8s/k8s-data.ts (mock consolidation) —
// behavior is identical: synchronous module-level data.
export * from '@/mocks/catalog/k8s';
