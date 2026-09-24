import { useMemo } from 'react';
import { listNetworks, type Network } from '@/shared/api/mocks/handmade/networks';

/**
 * Локальный хук для VPC-данных — стенд-ин пока нет kubb-эндпоинта.
 * Тот же паттерн, что `useListDbClusters`. Когда Phase X добавит
 * /api/v1/networks, модуль переедет на kubb-генерированный query, а
 * сигнатура хука (network list + counts) останется.
 */
export function useNetworks(): {
  networks: Network[];
  activeCount: number;
  totalSubnets: number;
  isPending: false;
  error: null;
} {
  return useMemo(() => {
    const networks = listNetworks();
    return {
      networks,
      activeCount: networks.filter((n) => n.status === 'active').length,
      totalSubnets: networks.reduce((acc, n) => acc + n.subnetCount, 0),
      isPending: false,
      error: null,
    };
  }, []);
}
