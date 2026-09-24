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
