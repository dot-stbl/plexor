import { useMemo } from 'react';
import { useNavigate } from '@tanstack/react-router';
import { useTranslation } from 'react-i18next';
import { PageTemplate } from '@/shared/ui/app-shell';
import { useEngine, useListDbClusters } from '../api/use-databases';
import { ManagedServiceListBody } from './managed-service-list-body';

/**
 * Страница одного managed-движка (раздел «Managed Service for X»): список
 * его кластеров + богатый онбординг, если их нет. Монтируется из route-файла
 * (/managed/<engine>) внутри layout-роута /managed через `<Outlet/>`.
 *
 * Тонкая data-shell: резолвит движок и его кластеры, рендерит общий
 * `ManagedServiceListBody` (strip + таблица + онбординг).
 */
export function ManagedServicePage({ engineId }: { engineId: string }) {
  const { t } = useTranslation();
  const navigate = useNavigate();
  const engine = useEngine(engineId);
  const { clusters, isPending } = useListDbClusters();
  const rows = useMemo(() => clusters.filter((cluster) => cluster.engineId === engineId), [clusters, engineId]);

  if (!engine) {
    return (
      <PageTemplate title={t('managed.engineNotFound.title')}>
        <p className="text-sm text-muted-foreground">{t('managed.engineNotFound.description', { engine: engineId })}</p>
      </PageTemplate>
    );
  }

  return (
    <ManagedServiceListBody
      engine={engine}
      clusters={rows}
      isPending={isPending}
      onCreate={() => void navigate({ to: '/managed/new', search: { engine: engine.id } })}
      onOpenCluster={(cluster) =>
        void navigate({
          to: '/managed/c/$clusterId',
          params: { clusterId: cluster.id },
        })
      }
    />
  );
}
