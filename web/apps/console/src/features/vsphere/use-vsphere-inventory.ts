import { useQueryClient } from '@tanstack/react-query';
import {
  getVSphereInventoryQueryKey,
  useGetVSphereInventory,
} from '@/shared/api';

/**
 * TanStack Query hook for GET /api/v1/vsphere/inventory. Wraps the
 * kubb-generated hook with the project's conventions:
 *   - re-export `data` as `inventory` for screen-friendliness
 *   - expose a 503-typed `error` so callers can switch on
 *     `inventory-not-configured` vs `inventory-empty`
 *   - the canonical `queryKey` lives in the generated module; the
 *     refresh + clone mutations invalidate it
 */
export function useVSphereInventory() {
  const query = useGetVSphereInventory();
  return {
    inventory: query.data,
    isPending: query.isPending,
    isError: query.isError,
    error: query.error,
    refetch: query.refetch,
  };
}

/**
 * Stable `queryKey` for the inventory — exported separately so
 * the refresh + clone mutations can invalidate it without reaching
 * into the generated module's internals.
 */
export function vsphereInventoryQueryKey(): readonly [
  { readonly url: '/vsphere/inventory' },
] {
  return getVSphereInventoryQueryKey() as ReturnType<typeof vsphereInventoryQueryKey>;
}

/**
 * Helper used by the refresh + clone mutations: invalidate the
 * cached inventory so the next mount / focus event refetches.
 */
export function useInvalidateVSphereInventory() {
  const queryClient = useQueryClient();
  return () => {
    void queryClient.invalidateQueries({ queryKey: vsphereInventoryQueryKey() });
  };
}
