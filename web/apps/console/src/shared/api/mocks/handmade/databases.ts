// to contracts/plexor.openapi.yaml, then regenerate
// (web/tooling/codegen), wire the kubb handler in
// shared/api/mocks/handlers.ts, and delete this module.
//
// Moved verbatim from features/databases/database-data.ts (mock
// consolidation) — behavior is identical: synchronous module-level data.
export * from '@/mocks/catalog/databases';
