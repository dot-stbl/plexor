/**
 * Public surface of the catalog domain (managed Kubernetes + managed
 * data services). Routes import from '@/domains/catalog'; internal
 * model/api/ui files stay unexported outside this barrel (see
 * .agents/docs/architecture/frontend-ddd.md).
 */
export type { K8sStatus, K8sCluster } from './model/k8s-types';
export { mapK8sStatusToVariant } from './model/k8s-types';
export { listK8s } from '@/shared/api/mocks/handmade/k8s';
export { getK8sColumns } from './ui/k8s-columns';
export { K8sListBody } from './ui/k8s-list-body';
export { K8sNoResultsState, K8sSkeleton } from './ui/k8s-states';
export {
  K8sStatusStrip,
  K8sStatusStripSkeleton,
  countByStatusFacet,
  sumResourceTotals,
  k8sStatusLabelKey,
  stripStatuses as k8sStripStatuses,
  isK8sStatus,
} from './ui/k8s-status-strip';
export type { K8sResourceTotals } from './ui/k8s-status-strip';
export { K8sDetailBody } from './ui/k8s-detail-body';
export { K8sDetailSkeleton, K8sDetailNotFound } from './ui/k8s-detail-states';

export type { Runtime, RuntimeClass, RuntimeOption, RuntimeHost, DbKind, DbStatus, DbEngine, DbCluster } from './model/database-types';
export { RUNTIME_ORDER, RUNTIME_META, DB_KIND_LABEL, mapDbStatusToVariant, availableRuntimes, runtimeOptions, defaultRuntime } from './model/database-types';
export { MANAGED_ROUTES, managedRoute } from './model/managed-routes';
export type { ManagedEngineId, ManagedRoute } from './model/managed-routes';
export { useListDbClusters, useEngines, useEngine, useRuntimeHosts } from './api/use-databases';
export { RuntimeBadge, RUNTIME_ICON } from './ui/runtime-badge';
export { RuntimePicker } from './ui/runtime-picker';
export { getDbColumns } from './ui/database-columns';
export { ManagedServiceEmpty } from './ui/managed-service-empty';
export { ManagedServicePage } from './ui/managed-service-page';
export { ManagedServiceListBody } from './ui/managed-service-list-body';
export {
  DbStatusStrip,
  DbStatusStripSkeleton,
  countDbByStatusFacet,
  sumDbTotals,
  dbStatusLabelKey,
  stripStatuses as dbStripStatuses,
  isDbStatus,
} from './ui/db-status-strip';
export type { DbTotals } from './ui/db-status-strip';
