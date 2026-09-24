import { useQueryClient } from '@tanstack/react-query';
import { listVmsQueryKey, useProvisionVm } from '@/shared/api';

/** Wraps kubb's `useProvisionVm` mutation, invalidating the VM list
 *  query on success so a freshly created VM appears in `/vms` (as
 *  `provisioning`, per the mock store's status progression) without a
 *  manual refresh. */
export function useCreateVm() {
  const queryClient = useQueryClient();
  return useProvisionVm({
    mutation: {
      onSuccess: () => {
        void queryClient.invalidateQueries({ queryKey: listVmsQueryKey() });
      },
    },
  });
}
