import type { StatusVariant } from '@/shared/ui/primitives/status-pill';
import type { VSphereInventoryVirtualMachineRowPowerStateEnumKey } from '@/shared/api';

/**
 * Wire enum (kubb-generated) → Plexor DS status variant. Closed map —
 * adding a new power state to the contract forces a compile error here.
 */
export function mapVSpherePowerStateToVariant(
  powerState: VSphereInventoryVirtualMachineRowPowerStateEnumKey,
): StatusVariant {
  switch (powerState) {
    case 'POWERED_ON':
      return 'running';
    case 'POWERED_OFF':
      return 'stopped';
    case 'SUSPENDED':
      return 'warn';
  }
}
