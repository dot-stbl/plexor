import type { StatusVariant } from '@/shared/ui/primitives/status-pill';
import type { VmStatus } from '@/shared/api';

/**
 * Wire enum (`VmStatus` from the API contract) → Plexor DS status variant.
 *
 * Single switch on the closed enum keeps the contract exhaustive — adding a
 * status to the API forces a compile error here until it is mapped.
 */
export function mapVmStatusToVariant(status: VmStatus): StatusVariant {
  switch (status) {
    case 'running':
      return 'running';
    case 'stopped':
      return 'stopped';
    case 'error':
      return 'err';
    case 'provisioning':
      return 'pending';
    case 'idle':
      return 'idle';
  }
}