/**
 * Public surface of the compute domain (VMs, LXC containers, OS image
 * catalog). Routes import from '@/domains/compute'; internal model/api/ui
 * files stay unexported outside this barrel (see
 * .agents/docs/architecture/frontend-ddd.md).
 */

// VMs
export { mapVmStatusToVariant } from './model/vm-status';
export { useListVms } from '@/shared/api';
export { VmBulkToolbar } from './ui/vm-bulk-toolbar';
export { VmRowActions } from './ui/vm-row-actions';
export { VmSkeleton, VmErrorBanner, VmEmptyState, VmNoResultsState } from './ui/vm-states';
export { getVmColumns } from './ui/vm-columns';
export {
  VmStatusStrip,
  VmStatusStripSkeleton,
  countByStatusFacet,
  isVmStatus,
  sumResourceTotals,
  vmStatusLabelKey,
} from './ui/vm-status-strip';
export type { VmResourceTotals } from './ui/vm-status-strip';
export { VmListBody } from './ui/vm-list-body';
export { VmConsoleCard } from './ui/vm-console-card';

// LXC
export * from './model/lxc-types';
export { listLxc } from '@/shared/api/mocks/handmade/lxc';
export { getLxcColumns } from './ui/lxc-columns';
export { LxcListBody } from './ui/lxc-list-body';
export { LxcNoResultsState, LxcSkeleton } from './ui/lxc-states';
export {
  LxcStatusStrip,
  LxcStatusStripSkeleton,
  countLxcByStatusFacet,
  sumLxcResourceTotals,
  lxcStatusLabelKey,
  stripStatuses as lxcStripStatuses,
  isLxcStatus,
} from './ui/lxc-status-strip';
export type { LxcResourceTotals } from './ui/lxc-status-strip';
export { LxcDetailBody } from './ui/lxc-detail-body';
export { LxcDetailSkeleton, LxcDetailNotFound } from './ui/lxc-detail-states';

// Images
export * from './model/image-types';
export { listImages } from './model/image-data';
export { getImageColumns } from './ui/image-columns';
export { ImageListBody } from './ui/image-list-body';
export {
  ImageStatusStrip,
  ImageStatusStripSkeleton,
  countImageByStatusFacet,
  sumImageTotals,
  imageStatusLabelKey,
  stripStatuses as imageStripStatuses,
  isImageStatus,
} from './ui/image-status-strip';
export type { ImageTotals } from './ui/image-status-strip';

// VM create wizard (in-flight wiring completion)
export type { VmWizardValues } from './model/vm-wizard';
export { mapVmWizardToCreateVmRequest } from './model/vm-wizard';
export { mapCreateVmErrorToFieldErrors } from './model/vm-create-errors';
export type { VmCreateFieldErrors } from './model/vm-create-errors';
export { useCreateVm } from './api/use-create-vm';

// vSphere (alternate compute provider — inventory read/refresh + template
// clone; see .agents/docs/architecture/*, issue #77)
export { mapVSpherePowerStateToVariant } from './model/vsphere-power-state';
export { useVSphereInventory, useInvalidateVSphereInventory } from './api/use-vsphere-inventory';
export { useVSphereRefresh } from './api/use-vsphere-refresh';
export { useVSphereClone } from './api/use-vsphere-clone';
export {
  getVSphereClusterColumns,
  getVSphereHostColumns,
  getVSphereVmColumns,
} from './ui/vsphere-inventory-columns';
export {
  VSphereInventorySkeleton,
  VSphereErrorBanner,
  VSphereEmptyState,
} from './ui/vsphere-states';
export { VSphereCloneForm } from './ui/vsphere-clone-form';
