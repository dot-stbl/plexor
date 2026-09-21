import type { VSphereCloneRequestBody } from '@/shared/api';
import { useCloneVSphereTemplate } from '@/shared/api';
import { useInvalidateVSphereInventory } from './use-vsphere-inventory';

/**
 * TanStack Query mutation hook for POST /api/v1/vsphere/clone.
 * The screen hands the request body to `mutate(body)`; on success
 * we invalidate the inventory cache so the new VM shows up in the
 * next list refresh (it won't be there until the user triggers a
 * refresh, but invalidation keeps the cache coherent if a background
 * refresh lands first).
 */
export function useVSphereClone() {
  const mutation = useCloneVSphereTemplate();
  const invalidate = useInvalidateVSphereInventory();

  return {
    ...mutation,
    mutate: (body: VSphereCloneRequestBody) => {
      mutation.mutate({ data: body }, { onSuccess: invalidate });
    },
  };
}
