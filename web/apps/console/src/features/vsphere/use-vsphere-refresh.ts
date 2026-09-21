import { toast } from 'sonner';
import { useRefreshVSphereInventory } from '@/shared/api';
import { useInvalidateVSphereInventory } from './use-vsphere-inventory';

/**
 * TanStack Query mutation hook for POST /api/v1/vsphere/inventory/refresh.
 * On success: invalidates the inventory query so the next read
 * fetches the freshly-pulled snapshot, and surfaces a success toast.
 * The mutation itself returns the kubb response body so callers can
 * inspect the new snapshot id if needed.
 */
export function useVSphereRefresh() {
  const mutation = useRefreshVSphereInventory();
  const invalidate = useInvalidateVSphereInventory();

  return {
    ...mutation,
    mutate: () => {
      mutation.mutate(undefined, {
        onSuccess: () => {
          invalidate();
          toast.success('vSphere inventory refreshed');
        },
      });
    },
  };
}
