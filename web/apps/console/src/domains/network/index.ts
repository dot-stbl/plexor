/**
 * Public surface of the network domain. Routes import from
 * '@/domains/network'; internal api/ui files stay unexported outside
 * this barrel (see .agents/docs/architecture/frontend-ddd.md).
 */
export { useNetworks } from './api/use-networks';
export type { Network, NetworkStatus } from '@/shared/api/mocks/handmade/networks';
export { getNetworkColumns } from './ui/network-columns';
export { NetworksEmpty } from './ui/networks-empty';