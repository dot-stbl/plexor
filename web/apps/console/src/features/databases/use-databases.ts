import { useMemo } from 'react';
import type { DbEngine, DbCluster, RuntimeHost } from './database-types';
import { getEngine, listDbClusters, listEngines, listRuntimeHosts } from './database-data';

/**
 * Локальные хуки — стенд-ин для kubb-хуков, пока нет OpenAPI-спеки
 * (тот же паттерн, что features/clusters).
 */
export function useListDbClusters(): { clusters: DbCluster[]; isPending: false; error: null } {
  return useMemo(() => ({ clusters: listDbClusters(), isPending: false, error: null }), []);
}

export function useEngines(): { engines: DbEngine[]; isPending: false } {
  return useMemo(() => ({ engines: listEngines(), isPending: false }), []);
}

export function useEngine(id: string | undefined): DbEngine | undefined {
  return useMemo(() => (id ? getEngine(id) : undefined), [id]);
}

export function useRuntimeHosts(): { hosts: RuntimeHost[]; isPending: false } {
  return useMemo(() => ({ hosts: listRuntimeHosts(), isPending: false }), []);
}
