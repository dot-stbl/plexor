/**
 * Каждый движок — отдельный раздел (как «Managed Service for X» в YC).
 * Карта engineId → его route для типобезопасных <Link>/navigate.
 */
export const MANAGED_ROUTES = {
  postgres: '/managed/postgres',
  redis: '/managed/redis',
  garnet: '/managed/garnet',
  clickhouse: '/managed/clickhouse',
  kafka: '/managed/kafka',
} as const;

export type ManagedEngineId = keyof typeof MANAGED_ROUTES;
export type ManagedRoute = (typeof MANAGED_ROUTES)[ManagedEngineId];

export function managedRoute(engineId: string): ManagedRoute {
  return (MANAGED_ROUTES as Record<string, ManagedRoute>)[engineId] ?? MANAGED_ROUTES.postgres;
}
