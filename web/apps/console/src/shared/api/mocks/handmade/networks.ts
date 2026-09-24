// Handmade mock — networks page data for the self-hosted edition.
//
// Plexor self-hosted has no /api/v1/networks endpoint yet (the network
// stack is still landing; see `.planning/`). This module exposes the
// page shape we'll wire once the kubb contract lands: a list of VPCs
// with their CIDR, subnet count, binding count and status. The empty
// case (no VPCs) is the realistic first-run state — see `NetworksEmpty`.
//
// TODO(contract): replace this with kubb-generated handlers and delete
// this module. The page already goes through `useNetworks()` so the
// migration is one file.
export * from '@/mocks/network/networks';
