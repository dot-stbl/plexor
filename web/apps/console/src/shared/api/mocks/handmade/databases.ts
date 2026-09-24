// Handmade mock — page data for endpoints NOT yet in the OpenAPI contract.
//
// TODO(contract): add the managed-data-service endpoints to
// contracts/plexor.openapi.yaml (engine catalog GET /api/v1/data/engines,
// deployed clusters GET /api/v1/data/clusters, runtime hosts
// GET /api/v1/data/runtime-hosts — final paths TBD), then regenerate
// (web/tooling/codegen), wire the kubb handlers in
// shared/api/mocks/handlers.ts, and delete this module.
//
// Moved verbatim from features/databases/database-data.ts (mock
// consolidation) — behavior is identical: synchronous module-level data.
export * from '@/mocks/catalog/databases';
